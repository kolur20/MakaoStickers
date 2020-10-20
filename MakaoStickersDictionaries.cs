using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Resto.Front.Api.MakaoStickers.Settings;
using System.Net;
using System.IO;

namespace Resto.Front.Api.MakaoStickers
{
    class MakaoStickersDictionaries
    {
        //************************************************************************************************************
        // получение справочника номенклатуры
        public static Dictionary<string, string> GetNomenclatureDictionary()
        {
            
            string RawNom = "";
            Dictionary<string, string> res = new Dictionary<string, string>();

            try
            {
                string URL = String.Format("http://{0}:{1}/{2}/service/export/csv/goods.csv",
                                            MakaoStickersConfig.Instance.iiko_Host,
                                            MakaoStickersConfig.Instance.iiko_Port,
                                            MakaoStickersConfig.Instance.iiko_Server);

                WebRequest myReq = WebRequest.Create(URL);

                //myReq.Headers.Add("Accept-Language", en.Name);
                myReq.Timeout = 1000000;
                string usernamePassword = MakaoStickersConfig.Instance.iiko_Login + ":" + MakaoStickersConfig.Instance.iiko_Password;
                CredentialCache mycache = new CredentialCache();
                mycache.Add(new Uri(URL), "Basic", new NetworkCredential(MakaoStickersConfig.Instance.iiko_Login, MakaoStickersConfig.Instance.iiko_Password));
                myReq.Credentials = mycache;
                myReq.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Unicode.GetBytes(usernamePassword)));
                WebResponse wr = myReq.GetResponse();
                Stream receiveStream = wr.GetResponseStream();
                StreamReader reader = new StreamReader(receiveStream, Encoding.UTF8);
                var response = (HttpWebResponse)myReq.GetResponse();
                RawNom = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch
            {
                RawNom = "";
            }

            if (RawNom != "")
            {
               string[] NomArr = RawNom.Replace(Environment.NewLine, String.Empty)
                                    .Replace("\n", String.Empty)
                                    .Replace("\r", String.Empty)
                                    .Split(';');

                for (int i = 1; i < NomArr.Length / 16; i++)
                {
                    try
                    {
                        if (NomArr[i * 16 + 0].ToUpper() == "GROUP")
                            continue;
                       
                        res.Add(NomArr[i * 16 + 1], NomArr[i * 16 + 3]);

                        // debug - проверять коллизии артикулов на всякий случай!!!
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return res;
        }


        //************************************************************************************************************
        // получение справочника ТТК
        public static Dictionary<string, List<string>> GetTTKDictionary()
        {
            string RawTTK = "";
            Dictionary<string, List<string>> res = new Dictionary<string, List<string>>();

            // качаем ТТК
            try
            {
                string URL = String.Format("http://{0}:{1}/{2}/service/export/csv/assemblyCharts.csv",
                                            MakaoStickersConfig.Instance.iiko_Host,
                                            MakaoStickersConfig.Instance.iiko_Port,
                                            MakaoStickersConfig.Instance.iiko_Server);

                WebRequest myReq = WebRequest.Create(URL);

                //myReq.Headers.Add("Accept-Language", en.Name);
                myReq.Timeout = 1000000;
                string usernamePassword = MakaoStickersConfig.Instance.iiko_Login + ":" + MakaoStickersConfig.Instance.iiko_Password;
                CredentialCache mycache = new CredentialCache();
                mycache.Add(new Uri(URL), "Basic", new NetworkCredential(MakaoStickersConfig.Instance.iiko_Login, MakaoStickersConfig.Instance.iiko_Password));
                myReq.Credentials = mycache;
                myReq.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Unicode.GetBytes(usernamePassword)));
                WebResponse wr = myReq.GetResponse();
                Stream receiveStream = wr.GetResponseStream();
                StreamReader reader = new StreamReader(receiveStream, Encoding.UTF8);
                var response = (HttpWebResponse)myReq.GetResponse();
                RawTTK = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch
            {
                RawTTK = "";
            }

            // парсим ТТК
            if (RawTTK != "")
            {
                string[] TTKArr = RawTTK.Replace(Environment.NewLine, String.Empty)
                                    .Replace("\n", String.Empty)
                                    .Replace("\r", String.Empty)
                                    .Split(';');

                try
                {
                    res.Add(TTKArr[9], new List<string>());
                }
                catch
                {
                    return res;
                }                

                for (int i = 2; i < TTKArr.Length / 9; i++)
                {
                    try
                    {
                        var lastTtk = res.Last();

                        if (TTKArr[i * 9 + 0] == lastTtk.Key)
                        {
                            lastTtk.Value.Add(TTKArr[i * 9 + 1]);
                        }
                        else
                        {
                            res.Add(TTKArr[i * 9 + 0], new List<string>());
                            res[TTKArr[i * 9 + 0]].Add(TTKArr[i * 9 + 1]);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return res;
        }

    }
}
