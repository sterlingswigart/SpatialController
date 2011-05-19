using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SpatialController
{
    /// <summary>
    /// Interaction logic for UserPrompt.xaml
    /// </summary>
    public partial class UserPrompt : Window
    {
        private static string LastMessage { get; set; }
        private static UserPrompt Active { get; set; }

        public UserPrompt()
        {
            InitializeComponent();
            Active = this;
        }

        private delegate void WriteDelegate(string text);
        public static void Write(string text)
        {
            if (Active.display.Dispatcher.CheckAccess())
            {
                if (text == LastMessage)
                    return;
                LastMessage = text;

                Active.display.AppendText(text + '\n');
                Active.display.ScrollToEnd();
            }
            else
            {
                Active.display.Dispatcher.Invoke(
                    DispatcherPriority.Normal,
                    new WriteDelegate(Write), text);
            }
        }
    }
}
