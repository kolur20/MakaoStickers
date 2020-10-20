using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using JetBrains.Annotations;
using Resto.Front.Api.MakaoStickers.Settings;
using Resto.Front.Api.V4;
using Resto.Front.Api.V4.Attributes;
using System.IO;

using System.Text;
using System.Windows.Forms;

namespace Resto.Front.Api.MakaoStickers
{
    [UsedImplicitly]
    //[PluginLicenseModuleId(21005108)]
    //[PluginLicenseModuleId(10000)]
    [PluginLicenseModuleId(21015808)]
    public sealed class MakaoStickers : IFrontPlugin
    {
        // справочники номенклатуры и ТТК
        public Dictionary<string, string> NomDictionary;
        public Dictionary<string, List<string>> TTKDictionary;

        // подписки...
        private readonly Stack<IDisposable> subscriptions = new Stack<IDisposable>();



        //****************************************************************************************************
        // конструктор
        public MakaoStickers()
        {
            PluginContext.Log.Info("Инициализация MakaoStickers...");
            
            /*
            //------------------------------------------------------------------------------------------------------------
            int DemoRes = CheckEndOfDemoLicense(DateTime.Parse("01.01.2017"));
            if (DemoRes == -1)
            {
                PluginContext.Log.Info("Для корректной работы текущей версии плагина требуется подключение к интернету.");
                PluginContext.Operations.AddWarningMessage("Для корректной работы текущей версии плагина требуется подключение к интернету.", "MakaoStickers");
                return;
            }
            else
            {
                if (DemoRes == 0)
                {
                    PluginContext.Log.Info("Время использования текущей версии плагина истекло. За дополнительной информацией обратитесь к разработчикам.");
                    PluginContext.Operations.AddWarningMessage("Время использования текущей версии плагина истекло. За дополнительной информацией обратитесь к разработчикам.", "MakaoStickers");
                    return;
                }
            }
            //------------------------------------------------------------------------------------------------------------
            */
            // читаем/генерим конфиг
            MakaoStickersConfig.Init(PluginContext.Integration.GetConfigsDirectoryPath());

            // качаем справочник номенклатуры. Вываливаемся если пусто
            PluginContext.Log.Info("Загрузка справочника номенклатуры сервера " + MakaoStickersConfig.Instance.iiko_Host);
            NomDictionary =  MakaoStickersDictionaries.GetNomenclatureDictionary();
            if (NomDictionary.Count == 0)
            {
                PluginContext.Log.Error("Не удалось загрузить справочник номенклатуры сервера " + MakaoStickersConfig.Instance.iiko_Host + ". Плагин остановлен.");
                PluginContext.Operations.AddErrorMessage("Ошибка запуска плагина печати наклеек. Подробнее - в логах.", "MakaoStickers");
                Environment.Exit(0);
            }
            else
            {
                PluginContext.Log.Info($"Cправочник номенклатуры составляет {NomDictionary.Count} позиций");
            }
            // качаем справочник ТТК. Вываливаемся ли если пусто?? Пока - да, вываливаемся. Что-то должно быть все-таки.
            PluginContext.Log.Info("Загрузка справочника ТТК сервера " + MakaoStickersConfig.Instance.iiko_Host);
            TTKDictionary = MakaoStickersDictionaries.GetTTKDictionary();
            if (TTKDictionary.Count == 0)
            {
                PluginContext.Log.Error("Не удалось загрузить справочник ТТК сервера" + MakaoStickersConfig.Instance.iiko_Host + ". Плагин остановлен.");
                PluginContext.Operations.AddErrorMessage("Ошибка запуска плагина печати наклеек. Подробнее - в логах.", "MakaoStickers");
                Environment.Exit(0);
            }
            else
            {
                PluginContext.Log.Info($"Cправочник ТТК составляет {TTKDictionary.Count} позиций");
            }
            // подшиваемся к печати пречека
            subscriptions.Push(new BillChequeExtender(NomDictionary, TTKDictionary));
     
            PluginContext.Operations.AddNotificationMessage("Плагин печати наклеек успешно стартовал.", "MakaoStickers");
            PluginContext.Log.Info("MakaoStickers стартовал.");
        }

        //****************************************************************************************************
        // деструктор
        public void Dispose()
        {
            while (subscriptions.Any())
            {
                var subscription = subscriptions.Pop();
                try
                {
                    subscription.Dispose();
                }
                catch (RemotingException)
                { }
            }

            PluginContext.Log.Info("MakaoStickers остановлен.");
        }

//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        //************************************************************************************************************
        // проверка по времени. Для демок.
        // -1 - не удалось проверить дату. Коннект или сервера, не важно - ругаемся на коннект.
        // 0 - время и стекло. Ругаемся отдельно.
        // >0 - все в порядке, продолжаем юзать демо-лицензию
        static public int CheckEndOfDemoLicense(DateTime EndDate)
        {
            int res = 1;
            try
            {
                DateTime NowDate = DateTime.Now;//GetFastestNISTDate();
                DateTime FailDate = DateTime.Parse("01.01.0001");

                // не удалось подключиться или все сервера вповалку...
                if (NowDate <= FailDate)
                {
                    res = -1;
                    return res;
                }
                else
                {
                    // время и стекло
                    if (NowDate >= EndDate)
                    {
                        res = 0;
                        return res;
                    }
                }
            }
            // явно проблемы коннекта
            catch
            {
                res = -1;
                return res;
            }

            return res;
        }



        //************************************************************************************************************
        public static DateTime GetFastestNISTDate()
        {
            var result = DateTime.MinValue;

            // Initialize the list of NIST time servers
            // http://tf.nist.gov/tf-cgi/servers.cgi
            string[] servers = new string[] 
            {
                "nist1-ny.ustiming.org",
                "nist1-nj.ustiming.org",
                "nist1-pa.ustiming.org",
                "time-a.nist.gov",
                "time-b.nist.gov",
                "nist1.aol-va.symmetricom.com",
                "nist1.columbiacountyga.gov",
                "nist1-chi.ustiming.org",
                "nist.expertsmi.com",
                "nist.netservicesgroup.com"
            };

            // Try 5 servers in random order to spread the load
            Random rnd = new Random();
            foreach (string server in servers.OrderBy(s => rnd.NextDouble()).Take(5))
            {
                try
                {
                    // Connect to the server (at port 13) and get the response
                    string serverResponse = string.Empty;
                    using (var reader = new StreamReader(new System.Net.Sockets.TcpClient(server, 13).GetStream()))
                    {
                        serverResponse = reader.ReadToEnd();
                    }

                    // If a response was received
                    if (!string.IsNullOrEmpty(serverResponse))
                    {
                        // Split the response string ("55596 11-02-14 13:54:11 00 0 0 478.1 UTC(NIST) *")
                        string[] tokens = serverResponse.Split(' ');

                        // Check the number of tokens
                        if (tokens.Length >= 6)
                        {
                            // Check the health status
                            string health = tokens[5];
                            if (health == "0")
                            {
                                // Get date and time parts from the server response
                                string[] dateParts = tokens[1].Split('-');
                                string[] timeParts = tokens[2].Split(':');

                                // Create a DateTime instance
                                DateTime utcDateTime = new DateTime(
                                    Convert.ToInt32(dateParts[0]) + 2000,
                                    Convert.ToInt32(dateParts[1]), Convert.ToInt32(dateParts[2]),
                                    Convert.ToInt32(timeParts[0]), Convert.ToInt32(timeParts[1]),
                                    Convert.ToInt32(timeParts[2]));

                                // Convert received (UTC) DateTime value to the local timezone
                                result = utcDateTime.ToLocalTime();

                                return result;
                                // Response successfully received; exit the loop

                            }
                        }

                    }

                }
                catch
                {
                    // Ignore exception and try the next server
                }
            }
            return result;
        }

//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

    }
}