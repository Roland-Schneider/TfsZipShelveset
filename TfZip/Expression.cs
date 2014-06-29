using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TfZip
{
    public class Expression
    {
        public string ExpressionType { get; set; } // And, Or, Rule
        public string Not { get; set; }
        public string FullNameRegex { get; set; }
        public string IsReadOnly { get; set; }
        public string IsXmlDocFile { get; set; }
        public ExpressionList Operands { get; set; }

        public bool Evaluate(FileInfo fileInfo)
        {
            bool result = true;
            switch (ExpressionType.ToUpper())
            {
                default:
                    throw new InvalidOperationException(String.Format("Invalid ExpressionType \"{0}\"", (ExpressionType != null) ? ExpressionType : "(null)"));
                case "AND":
                    {
                        bool first = true;
                        foreach (Expression subNode in Operands)
                        {
                            bool operandResult = subNode.Evaluate(fileInfo);
                            if (first)
                            {
                                result = operandResult;
                                first = false;
                            }
                            else
                            {
                                result = result && operandResult;
                            }
                            if (!result)
                            {
                                break;
                            }
                        }
                        if (first)
                        {
                            throw new InvalidOperationException("Empty Operand list");
                        }
                        break;
                    }
                case "OR":
                    {
                        bool first = true;
                        foreach (Expression subNode in Operands)
                        {
                            bool operandResult = subNode.Evaluate(fileInfo);
                            if (first)
                            {
                                result = operandResult;
                                first = false;
                            }
                            else
                            {
                                result = result || operandResult;
                            }
                            if (result)
                            {
                                break;
                            }
                        }
                        if (first)
                        {
                            throw new InvalidOperationException("Empty Operand list");
                        }
                        break;
                    }
                case "RULE":
                    {
                        if (FullNameRegex != null)
                        {
                            result = Regex.IsMatch(fileInfo.FullName, FullNameRegex, RegexOptions.IgnoreCase);
                        }
                        else if (IsReadOnly != null)
                        {
                            result = fileInfo.IsReadOnly;
                        }
                        else if (IsXmlDocFile != null)
                        {
                            if (Regex.IsMatch(fileInfo.FullName, @"\.xml\z", RegexOptions.IgnoreCase))
                            {
                                string secondLine = File.ReadLines(fileInfo.FullName).ElementAtOrDefault(1);
                                result = ((secondLine != null) && Regex.IsMatch(secondLine, @"^<doc>$"));
                            }
                            else
                            {
                                result = false;
                            }
                        }
                        else
                        {
                            return true;
                        }
                        break;
                    }
            }
            if (Not != null)
            {
                result = !result;
            }
            return result;
        }
    }


    public class ExpressionList : Collection<Expression>
    {
    }
}
