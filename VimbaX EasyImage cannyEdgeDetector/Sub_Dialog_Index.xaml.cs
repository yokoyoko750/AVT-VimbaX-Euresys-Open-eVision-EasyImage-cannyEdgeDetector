using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VimbaX_EasyImage_cannyEdgeDetector
{
    /// <summary>
    /// Interaction logic for Sub_Dialog_Index.xaml
    /// </summary>
    public partial class Sub_Dialog_Index : Window
    {
        public int iIndex=0;

        public Sub_Dialog_Index()
        {
            InitializeComponent();
            Label_Value.Text = iIndex.ToString();
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            int iTemp = int.Parse(Label_Value.Text);
            if (iTemp < 0)
                iTemp = 0;
            else if (iTemp > 999)
                iTemp = 999;
            iIndex = iTemp;
            Close();
        }
    }
}
