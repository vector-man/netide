﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetIde.Shell.Interop;

namespace NetIde.Shell
{
    public abstract class NiPackage : ServiceObject, INiPackage
    {
        private System.Resources.ResourceManager _stringResourceManager;
        private NiServiceContainer _serviceContainer;
        private readonly Dictionary<Type, NiWindowPane> _toolWindows = new Dictionary<Type, NiWindowPane>();
        private int _commandTargetCookie;
        private bool _disposed;

        public event CancelEventHandler PackageClosing;

        protected virtual void OnPackageClosing(CancelEventArgs e)
        {
            var ev = PackageClosing;
            if (ev != null)
                ev(this, e);
        }

        protected NiPackage()
        {
            AppDomainSetup.Setup();
        }

        public string ResolveStringResource(string key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (key.StartsWith("@"))
            {
                string value;
                ErrorUtil.ThrowOnFailure(GetStringResource(key.Substring(1), out value));
                return value;
            }

            return key;
        }

        public HResult GetStringResource(string key, out string value)
        {
            value = null;

            try
            {
                if (key == null)
                    throw new ArgumentNullException("key");

                EnsureStringResources();

                value = _stringResourceManager.GetString(key);

                if (String.IsNullOrEmpty(value))
                    throw new ArgumentException(String.Format(Labels.InvalidResource, key));

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        private void EnsureStringResources()
        {
            if (_stringResourceManager != null)
                return;

            var attribute = GetType().GetCustomAttributes(typeof(NiStringResourcesAttribute), false);

            if (attribute.Length == 0)
                throw new InvalidOperationException(Labels.CouldNotFindStringResourcesAttribute);

            string resourceName = GetType().Namespace + "." + ((NiStringResourcesAttribute)attribute[0]).ResourceName;

            _stringResourceManager = new System.Resources.ResourceManager(resourceName, GetType().Assembly);
        }

        public HResult GetNiResources(out IResource value)
        {
            value = null;

            try
            {
                var attribute = GetType().GetCustomAttributes(typeof(NiResourcesAttribute), false);

                if (attribute.Length == 0)
                    throw new InvalidOperationException(Labels.CouldNotFindResourcesAttribute);

                string resourceName = GetType().Namespace + "." + ((NiResourcesAttribute)attribute[0]).ResourceName + ".resources";

                if (!GetType().Assembly.GetManifestResourceNames().Contains(resourceName))
                    return HResult.False;

                value = ResourceUtil.FromManifestResourceStream(
                    GetType().Assembly,
                    resourceName
                );

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public virtual HResult Initialize()
        {
            try
            {
                var commandTarget = this as INiCommandTarget;

                if (commandTarget != null)
                    ((INiCommandManager)GetService(typeof(INiCommandManager))).RegisterCommandTarget(commandTarget, out _commandTargetCookie);

                RegisterEditorFactories();

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        private void RegisterEditorFactories()
        {
            var registry = (INiLocalRegistry)GetService(typeof(INiLocalRegistry));

            foreach (ProvideEditorFactoryAttribute attribute in GetType().GetCustomAttributes(typeof(ProvideEditorFactoryAttribute), true))
            {
                var editorFactory = (INiEditorFactory)Activator.CreateInstance(attribute.EditorType);

                editorFactory.SetSite(this);

                ErrorUtil.ThrowOnFailure(registry.RegisterEditorFactory(
                    attribute.EditorType.GUID,
                    editorFactory
                ));
            }
        }

        public HResult SetSite(IServiceProvider serviceProvider)
        {
            try
            {
                if (serviceProvider == null)
                    throw new ArgumentNullException("serviceProvider");

                // This is the first time we get access to an IServiceProvider
                // from a new AppDomain. Install logging redirection now.

                LoggingRedirection.Install(serviceProvider);

                _serviceContainer = new NiServiceContainer(serviceProvider);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        [DebuggerStepThrough]
        public object GetService(Type serviceType)
        {
            return _serviceContainer.GetService(serviceType);
        }

        public NiWindowPane FindToolWindow(Type toolType, bool create)
        {
            if (toolType == null)
                throw new ArgumentNullException("toolType");

            NiWindowPane result;

            if (!_toolWindows.TryGetValue(toolType, out result) && create)
                result = CreateToolWindow(toolType);

            return result;
        }

        public HResult CreateToolWindow(Guid guid, out INiWindowPane toolWindow)
        {
            toolWindow = null;

            try
            {
                var registration = GetType()
                    .GetCustomAttributes(typeof(ProvideToolWindowAttribute), true)
                    .Cast<ProvideToolWindowAttribute>()
                    .SingleOrDefault(p => p.ToolType.GUID == guid);

                if (registration != null)
                {
                    toolWindow = CreateToolWindow(registration.ToolType);
                    return HResult.OK;
                }

                return HResult.False;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public NiWindowPane CreateToolWindow(Type toolType)
        {
            if (toolType == null)
                throw new ArgumentNullException("toolType");

            var registration = GetType()
                .GetCustomAttributes(typeof(ProvideToolWindowAttribute), true)
                .Cast<ProvideToolWindowAttribute>()
                .SingleOrDefault(p => p.ToolType == toolType);

            if (registration == null)
                throw new InvalidOperationException(Labels.ToolWindowNotRegistered);

            var toolWindow = (NiWindowPane)Activator.CreateInstance(toolType);

            ErrorUtil.ThrowOnFailure(toolWindow.SetSite(this));

            var shell = ((INiShell)GetService(typeof(INiShell)));

            INiWindowFrame frame;
            ErrorUtil.ThrowOnFailure(shell.CreateToolWindow(
                toolWindow,
                registration.Style,
                registration.Orientation,
                out frame
            ));

            toolWindow.Frame = frame;

            ErrorUtil.ThrowOnFailure(toolWindow.Initialize());

            _toolWindows[toolType] = toolWindow;

            new ToolWindowListener(toolWindow).Closed += toolWindow_Closed;

            return toolWindow;
        }

        void toolWindow_Closed(object sender, EventArgs e)
        {
            _toolWindows.Remove(((ToolWindowListener)sender).Window.GetType());
        }

        public HResult QueryClose(out bool canClose)
        {
            canClose = false;

            try
            {
                var ev = new CancelEventArgs();

                OnPackageClosing(ev);

                canClose = !ev.Cancel;

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult Register(INiRegistrationContext registrationContext)
        {
            try
            {
                if (registrationContext == null)
                    throw new ArgumentNullException("registrationContext");

                var packageGuid = GetType().GUID.ToString("B").ToUpperInvariant();

                var descriptionAttribute = GetType().GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>().SingleOrDefault();
                string description = descriptionAttribute != null ? descriptionAttribute.Description : registrationContext.PackageId;

                using (var key = registrationContext.CreateKey("Packages\\" + packageGuid))
                {
                    key.SetValue(null, ResolveStringResource(description));

                    foreach (var type in GetType().Assembly.GetTypes())
                    {
                        foreach (RegistrationAttribute attribute in type.GetCustomAttributes(typeof(RegistrationAttribute), true))
                        {
                            attribute.Register(this, registrationContext, key);
                        }
                    }
                }

                using (var key = registrationContext.CreateKey("InstalledProducts\\" + registrationContext.PackageId))
                {
                    key.SetValue(null, description);
                    key.SetValue("Package", packageGuid);
                    key.SetValue("Version", GetType().Assembly.GetName().Version.ToString());
                }

                // Add the installed products key.

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult Unregister(INiRegistrationContext registrationContext)
        {
            try
            {
                if (registrationContext == null)
                    throw new ArgumentNullException("registrationContext");

                var packageGuid = GetType().GUID.ToString("B").ToUpperInvariant();

                using (var key = registrationContext.CreateKey("Packages\\" + packageGuid))
                {
                    foreach (var type in GetType().Assembly.GetTypes())
                    {
                        foreach (RegistrationAttribute attribute in type.GetCustomAttributes(typeof(RegistrationAttribute), true))
                        {
                            attribute.Unregister(this, registrationContext, key);
                        }
                    }
                }

                // Un-registration allows attributes to perform cleanup other
                // than from inside the package namespace. The package namespace
                // itself is unconditionally deleted anyway, so the attributes
                // don't have to clean up that.

                registrationContext.RemoveKey("Packages\\" + packageGuid);
                registrationContext.RemoveKey("InstalledProducts\\" + registrationContext.PackageId);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_commandTargetCookie != 0)
                {
                    ((INiCommandManager)GetService(typeof(INiCommandManager))).UnregisterCommandTarget(_commandTargetCookie);
                    _commandTargetCookie = 0;
                }

                _disposed = true;
            }
        }

        private class ToolWindowListener : NiEventSink, INiWindowFrameNotify
        {
            public NiWindowPane Window { get; private set; }

            public event EventHandler Closed;

            private void OnClosed(EventArgs e)
            {
                var ev = Closed;
                if (ev != null)
                    ev(this, e);
            }

            public ToolWindowListener(NiWindowPane owner)
                : base(owner.Frame)
            {
                Window = owner;
            }

            public void OnShow(NiWindowShow action)
            {
                if (action == NiWindowShow.Close)
                {
                    OnClosed(EventArgs.Empty);

                    Dispose();
                }
            }

            public void OnSize()
            {
            }
        }
    }
}