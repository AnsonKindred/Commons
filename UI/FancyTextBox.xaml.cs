using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Commons.UI
{
    public partial class FancyTextBox : Border
    {
        public RoutedEvent? TextBoxGotFocus { get; set; }
        public RoutedEvent? TextBoxLostFocus { get; set; }

        public static DependencyProperty PlaceholderTextProperty;
        public static DependencyProperty TextProperty;

        static FancyTextBox()
        {
            PlaceholderTextProperty = DependencyProperty.Register("PlaceholderText", typeof(string), typeof(FancyTextBox));
            TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(FancyTextBox));
        }

        public string PlaceholderText {
            get { return (string)base.GetValue(PlaceholderTextProperty); }
            set { base.SetValue(PlaceholderTextProperty, value); }
        }

        public string Text {
            get { return (string)base.GetValue(TextProperty); }
            set { base.SetValue(TextProperty, value); }
        }

        public FancyTextBox() 
        {
            this.DataContext = this;
            InitializeComponent();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PlaceholderTextBlock.Visibility = Visibility.Hidden;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TextBox.Text == "")
            {
                PlaceholderTextBlock.Visibility = Visibility.Visible;
            }
        }
    }
}
