﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18444
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TfZip {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "12.0.0.0")]
    internal sealed partial class TfZipSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static TfZipSettings defaultInstance = ((TfZipSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new TfZipSettings())));
        
        public static TfZipSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("\r\n                    <Expression xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-ins" +
            "tance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n                        <E" +
            "xpressionType>Or</ExpressionType>\r\n                        <Not />\r\n            " +
            "            <Operands>\r\n                            <Expression>\r\n              " +
            "                  <ExpressionType>Rule</ExpressionType>\r\n                       " +
            "         <IsReadOnly />\r\n                            </Expression>\r\n            " +
            "                <Expression>\r\n                                <ExpressionType>Ru" +
            "le</ExpressionType>\r\n                                <FullNameRegex>\\\\obj\\\\debug" +
            "\\\\</FullNameRegex>\r\n                            </Expression>\r\n                 " +
            "           <Expression>\r\n                                <ExpressionType>Rule</E" +
            "xpressionType>\r\n                                <FullNameRegex>\\\\bin\\\\debug\\\\</F" +
            "ullNameRegex>\r\n                            </Expression>\r\n                      " +
            "      <Expression>\r\n                                <ExpressionType>Rule</Expres" +
            "sionType>\r\n                                <FullNameRegex>\\\\bin\\\\[^\\\\]+\\\\debug\\\\" +
            "</FullNameRegex>\r\n                            </Expression>\r\n                   " +
            "         <Expression>\r\n                                <ExpressionType>Rule</Exp" +
            "ressionType>\r\n                                <FullNameRegex>\\\\obj\\\\release\\\\</F" +
            "ullNameRegex>\r\n                            </Expression>\r\n                      " +
            "      <Expression>\r\n                                <ExpressionType>Rule</Expres" +
            "sionType>\r\n                                <FullNameRegex>\\\\bin\\\\release\\\\</Full" +
            "NameRegex>\r\n                            </Expression>\r\n                         " +
            "   <Expression>\r\n                                <ExpressionType>Rule</Expressio" +
            "nType>\r\n                                <FullNameRegex>\\\\bin\\\\[^\\\\]+\\\\release\\\\<" +
            "/FullNameRegex>\r\n                            </Expression>\r\n                    " +
            "        <Expression>\r\n                                <ExpressionType>Rule</Expr" +
            "essionType>\r\n                                <FullNameRegex>\\.suo\\z</FullNameReg" +
            "ex>\r\n                            </Expression>\r\n                            <Exp" +
            "ression>\r\n                                <ExpressionType>Rule</ExpressionType>\r" +
            "\n                                <IsXmlDocFile />\r\n                            <" +
            "/Expression>\r\n                        </Operands>\r\n                    </Express" +
            "ion>\r\n                ")]
        public global::TfZip.Expression LocalFilesSelectionConfiguration {
            get {
                return ((global::TfZip.Expression)(this["LocalFilesSelectionConfiguration"]));
            }
            set {
                this["LocalFilesSelectionConfiguration"] = value;
            }
        }
    }
}
