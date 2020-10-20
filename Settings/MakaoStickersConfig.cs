using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Resto.Front.Api.V4;
using System.Drawing;

namespace Resto.Front.Api.MakaoStickers.Settings
{
    [XmlRoot("Config")]
    public sealed class MakaoStickersConfig
    {
        private const string ConfigFileName = @"MakaoStickers.front.config.xml";

        //************************************************************************************************************
        public MakaoStickersConfig()
        { }

        private static string PathToConfigDir;

        //************************************************************************************************************
        private static string FilePath
        {
            get { return Path.GetFullPath(Path.Combine(PathToConfigDir, ConfigFileName)); }
        }

        [XmlElement]
        public string Restaurant_Department { get; set; }              // название заведения, статическая инфа в наклейке

        [XmlElement]
        public string Restaurant_Address { get; set; }                 // адрес, статическая инфа в наклейке

        [XmlElement]
        public string Restaurant_ExpirationTime { get; set; }         // общее время жизни товаров

        [XmlElement]
        public string Restaurant_StorageTemperature { get; set; }      // общая температура хранения

        [XmlElement]
        public int Restaurant_SpecialHall_StartTableInd { get; set; }  // номер первого стола в специальном зале

        [XmlElement]
        public int Restaurant_SpecialHall_EndTableInd { get; set; }     // номер последнего стола в специальном зале

        [XmlElement]
        public string iiko_Host { get; set; }           // хост iikkoo

        [XmlElement]
        public string iiko_Port { get; set; }           // порт iikkoo

        [XmlElement]
        public string iiko_Server { get; set; }         // имя сервера iikkoo

        [XmlElement]
        public string iiko_Login { get; set; }          // логин

        [XmlElement]
        public string iiko_Password { get; set; }       // пароль

        [XmlElement]
        public string Printer_Name { get; set; }        // имя принтера

        [XmlElement]
        public int Printer_PageWidth { get; set; }      // ширина бумаги в ПИКСЕЛЯХ!!!

        [XmlElement]
        public int Printer_PageHeight { get; set; }     // высота бумаги в ПИКСЕЛЯХ!!!

        [XmlElement]
        public float Printer_HorisontalBordersWidth { get; set; }     // ширина горизонтальных рамок в ПИКСЕЛЯХ!

        [XmlElement]
        public float Printer_VerticalBordersHeight { get; set; }      // высота вертикальных рамок в ПИКСЕЛЯХ!

        [XmlElement]
        public string Printer_BaseFont { get; set; }                 // основной шрифт

        [XmlElement]
        public string Printer_BoldFont { get; set; }                 // жирный шрифт

        [XmlArray]
        public List<BoldSubString> BoldSubstrings;                    // список выделяемых подстрок

        [XmlElement]
        public int Plugin_PrintOrderDelta { get; set; }              // минимальное теоретическое количество секунд, разделяющее две сессии печати заказа.

        private static MakaoStickersConfig instance;

        //************************************************************************************************************
        public static MakaoStickersConfig Instance
        {
            get
            {
                return instance ?? (instance = Load());
            }
        }

        //************************************************************************************************************
        private static MakaoStickersConfig Load()
        {
            try
            {
                PluginContext.Log.InfoFormat("Загрузка конфига MakaoStickers из файла {0}", FilePath);
                using (var stream = new FileStream(FilePath, FileMode.Open))
                using (var reader = new StreamReader(stream))
                {
                    return (MakaoStickersConfig)new XmlSerializer(typeof(MakaoStickersConfig)).Deserialize(reader);
                }
            }
            catch (Exception e)
            {
                PluginContext.Log.Error("Не удалось загрузить конфиг MakaoStickers. Используются настройки по умолчанию.", e);
            }

            var config = new MakaoStickersConfig();

            config.Restaurant_Department                = "НАЗВАНИЕ ПОДРАЗДЕЛЕНИЯ";
            config.Restaurant_Address                   = "АДРЕС ПОДРАЗДЕЛЕНИЯ";
            config.Restaurant_SpecialHall_StartTableInd = 0;
            config.Restaurant_SpecialHall_EndTableInd   = 0;
            config.Restaurant_ExpirationTime            = "НАПРИМЕР: 24 ч.";
            config.Restaurant_StorageTemperature        = "НАПРИМЕР: от +2°С до +4°С";

            config.iiko_Host        = "localhost";
            config.iiko_Port        = "8080";
            config.iiko_Server      = "resto";
            config.iiko_Login       = "admin";
            config.iiko_Password    = "resto#test";

            config.Printer_Name                     = "ИМЯ ПРИНТЕРА НАКЛЕЕК";
            config.Printer_PageWidth                = 180;
            config.Printer_PageHeight               = 270;
            config.Printer_HorisontalBordersWidth   = 4;
            config.Printer_VerticalBordersHeight    = 4;
            var cvt                                 = new FontConverter();
            config.Printer_BaseFont                 = cvt.ConvertToString(new Font("Arial", 7));
            config.Printer_BoldFont                 = cvt.ConvertToString(new Font("Arial", 8, FontStyle.Bold));

            config.BoldSubstrings = new List<BoldSubString>();

            config.Plugin_PrintOrderDelta           = 20;
            
            BoldSubString BSS = new BoldSubString();
            BSS.BSS = "ЭТОТ ТЕКСТ БУДЕТ ВЫДЕЛЕН. ДОБАВЬ ПОДОБНЫХ БЛОКОВ, ЕСЛИ НУЖНО ВЫДЕЛИТЬ ЕЩЕ ЧТО-НИБУДЬ";
            config.BoldSubstrings.Add(BSS);

            BoldSubString BSS1 = new BoldSubString();
            BSS1.BSS = "ВОТ ТАКИМ ОБРАЗОМ ДОБАВЬ";
            config.BoldSubstrings.Add(BSS1);

            BoldSubString BSS2 = new BoldSubString();
            BSS2.BSS = "А ЭТИ БЛОКИ НЕ ЗАБУДЬ УДАЛИТЬ";
            config.BoldSubstrings.Add(BSS2);
    
            config.Save();
            return config;
        }

        //************************************************************************************************************
        public void Save()
        {
            try
            {
                PluginContext.Log.InfoFormat("Сохранение когфига MakaoStickers в файл {0}", FilePath);
                using (Stream stream = new FileStream(FilePath, FileMode.Create))
                {
                    new XmlSerializer(typeof(MakaoStickersConfig)).Serialize(stream, this);
                }
            }
            catch (Exception e)
            {
                PluginContext.Log.Error("Не удалось сохранить конфиг MakaoStickersg.", e);
            }
        }

        //************************************************************************************************************
        public static void Init(string pathToConfigDir)
        {
            PathToConfigDir = pathToConfigDir;
        }
    }


    //************************************************************************************************************
    [XmlRoot("BoldSubString")]
    public class BoldSubString
    {
        [XmlElement]
        public string BSS;
        
        //************************************************************************************************************
        public void Save(string FilePath)
        {
            try
            {
                using (Stream stream = new FileStream(FilePath, FileMode.Create))
                {
                    new XmlSerializer(typeof(BoldSubString)).Serialize(stream, this);
                }
            }
            catch
            { }
        }

        //************************************************************************************************************
        public static BoldSubString Load(string FilePath)
        {
            try
            {
                using (var stream = new FileStream(FilePath, FileMode.Open))
                using (var reader = new StreamReader(stream))
                {
                    return (BoldSubString)new XmlSerializer(typeof(BoldSubString)).Deserialize(reader);
                }
            }
            catch
            { }
            return null;
        }

    }
}