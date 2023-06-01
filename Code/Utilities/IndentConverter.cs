using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace MediFiler_V2
{
    class IndentConverter : IValueConverter
    {
        private const int Indent = 15;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int level = (int)value;
            return new Thickness(level * Indent, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
