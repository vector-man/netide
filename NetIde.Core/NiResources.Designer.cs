﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18033
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NetIde.Core {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class NiResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal NiResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NetIde.Core.NiResources", typeof(NiResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap Exit {
            get {
                object obj = ResourceManager.GetObject("Exit", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;!--
        ///    This code was generated by a tool.
        ///
        ///    Changes to this file may cause incorrect behavior and will be lost if
        ///    the code is regenerated.
        ///--&gt;
        ///&lt;resources xmlns=&quot;https://github.com/pvginkel/NetIde/schemas/Resources&quot;&gt;
        ///  &lt;ui&gt;
        ///    &lt;menu id=&quot;Menu_File&quot; text=&quot;@MenuFile&quot; priority=&quot;100&quot; kind=&quot;Menu&quot; guid=&quot;{047F8695-7B12-41D8-8FF3-82F3416A9E5D}&quot;&gt;
        ///      &lt;group priority=&quot;100&quot;&gt;
        ///        &lt;button id=&quot;File_Exit&quot; priority=&quot;100&quot; text=&quot;@MenuFileExit&quot; key=&quot;Alt+F4&quot; image=&quot;@Exit&quot; guid=&quot;{5E1BBD55-8D4A-4CE7-A504- [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string NiMenu {
            get {
                return ResourceManager.GetString("NiMenu", resourceCulture);
            }
        }
    }
}
