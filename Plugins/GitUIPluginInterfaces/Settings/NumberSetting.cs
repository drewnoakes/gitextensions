﻿using System.Windows.Forms;
using GitExtUtils;

namespace GitUIPluginInterfaces
{
    public class NumberSetting<T> : ISetting
    {
        public NumberSetting(string name, T defaultValue)
            : this(name, name, defaultValue)
        {
        }

        public NumberSetting(string name, string caption, T defaultValue)
        {
            Name = name;
            Caption = caption;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public string Caption { get; }
        public T DefaultValue { get; }
        public TextBox? CustomControl { get; set; }

        public ISettingControlBinding CreateControlBinding()
        {
            return new TextBoxBinding(this, CustomControl);
        }

        private class TextBoxBinding : SettingControlBinding<NumberSetting<T>, TextBox>
        {
            public TextBoxBinding(NumberSetting<T> setting, TextBox? customControl)
                : base(setting, customControl)
            {
            }

            public override TextBox CreateControl()
            {
                Setting.CustomControl = new TextBox();
                return Setting.CustomControl;
            }

            public override void LoadSetting(ISettingsSource settings, TextBox control)
            {
                object? settingVal = settings.SettingLevel == SettingLevel.Effective
                    ? Setting.ValueOrDefault(settings)
                    : Setting[settings];

                control.Text = ConvertToString(settingVal);
            }

            public override void SaveSetting(ISettingsSource settings, TextBox control)
            {
                var controlValue = control.Text;

                if (settings.SettingLevel == SettingLevel.Effective)
                {
                    if (ConvertToString(Setting.ValueOrDefault(settings)) == controlValue)
                    {
                        return;
                    }
                }

                Setting[settings] = ConvertFromString(controlValue);
            }
        }

        private static string ConvertToString(object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        private static object? ConvertFromString(string value)
        {
            if (Strings.IsNullOrEmpty(value))
            {
                return null;
            }

            var type = typeof(T);
            if (type == typeof(int))
            {
                return int.Parse(value);
            }

            if (type == typeof(float))
            {
                return float.Parse(value);
            }

            if (type == typeof(double))
            {
                return double.Parse(value);
            }

            if (type == typeof(long))
            {
                return long.Parse(value);
            }

            return null;
        }

        public object? this[ISettingsSource settings]
        {
            get
            {
                return settings.GetValue(Name, null, s =>
                    {
                        return ConvertFromString(s);
                    });
            }

            set
            {
                settings.SetValue(Name, value, i => { return ConvertToString(i); });
            }
        }

        public T ValueOrDefault(ISettingsSource settings)
        {
            object? settingVal = this[settings];
            if (settingVal is null)
            {
                return DefaultValue;
            }
            else
            {
                return (T)settingVal;
            }
        }
    }
}
