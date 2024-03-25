#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Stylet;
using VSDownloader.ViewModels;

#endregion

namespace VSDownloader
{
    internal class Bootstrapper : Bootstrapper<ShellViewModel>
    {
        #region Methods

        protected override void Configure()
        {
            base.Configure();
            Ioc.GetInstance = Container.Get;
            Ioc.GetAllInstance = Container.GetAll;
        }

        protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            base.OnUnhandledException(e);
            if (Application.MainWindow != null)
            {
                MessageBox.Show(Application.MainWindow, e.Exception.GetBaseException().Message, "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion
    }

    internal class Ioc
    {
        public static Func<Type, string, IEnumerable<object>>
            GetAllInstance = (type, key) => throw new NotImplementedException();

        public static Func<Type, string, object> GetInstance = (type, key) => throw new NotImplementedException();

        public static T Get<T>(string key = null)
        {
            return (T)GetInstance(typeof(T), key);
        }

        public static IEnumerable<T> GetAll<T>(string key = null)
        {
            return GetAllInstance(typeof(T), key).Cast<T>();
        }
    }
}