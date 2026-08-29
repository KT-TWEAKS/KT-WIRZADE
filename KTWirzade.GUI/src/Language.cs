using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace KTWirzade.GUI
{
    public class Language : INotifyPropertyChanged
    {
        public string Title { get; set; }

        public string LangCode { get; set; }

        private bool _isOptional { get; set; }

        private bool _isSelected { get; set; }

        public bool IsOptional
        {
            get
            {
                return _isOptional;
            }
            set
            {
                _isOptional = value;
                OnPropertyChanged("IsOptional");
            }
        }

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static List<Language> GetLanguages()
        {
            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            List<string> enabledLanguages = new List<string>
        {
            "en-US", "nb-NO", "nl-NL", "zh-CN", "zh-TW", "fr-FR", "de-DE", "ja-JP", "it-IT", "sv-SE",
            "ko-KR", "es-ES", "fi-FI", "pt-BR", "pt-PT", "ru-RU"
        };
            List<string> languagesInstalled = (from InputLanguage lang in InputLanguage.InstalledInputLanguages
                                               select lang.Culture.IetfLanguageTag).ToList();
            return (from culture in cultures.Skip(1)
                    where enabledLanguages.Contains(culture.IetfLanguageTag)
                    select new Language
                    {
                        Title = culture.EnglishName,
                        LangCode = culture.IetfLanguageTag,
                        IsOptional = true,
                        IsSelected = false
                    } into lang
                    orderby lang.IsOptional
                    select lang).Select(delegate (Language lang)
                    {
                        if (!languagesInstalled.Contains(lang.LangCode))
                        {
                            return lang;
                        }
                        lang.IsSelected = true;
                        lang.IsOptional = false;
                        return lang;
                    }).ToList();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}