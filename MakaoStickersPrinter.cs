using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing.Printing;
using System.Drawing;
using Resto.Front.Api.MakaoStickers.Settings;
using Resto.Front.Api.V4;

namespace Resto.Front.Api.MakaoStickers
{
    class MakaoStickersPrinter
    {

        #region GLOBALSINTERNALS
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        // глобальный список юнитов, для доступности при многостраничной печати. Сбрасывается при каждой новой печати.
        private List <StickersStringUnit> StringsUnitsList = null;
        
        // глобальный курсор по этому списку, опять же. Сбрасывается при каждой новой печати.
        private int CurrentSUIndex = 0;   
        
        // шрифты. Стандартный и жирный, соответственно
        Font printFontBase;// = new Font("Arial", 7);
        Font printFontBold;// = new Font("Arial", 8, FontStyle.Bold);              

        // объекты для вычисления физической длины строк
        Bitmap CalcBitmap;
        Graphics CalcGraphics;


       
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************
        #endregion


       #region GLOBALSINTERNALS
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        //************************************************************************************************************
        public MakaoStickersPrinter()
        {
            CalcBitmap = new Bitmap(
                                    (int) MakaoStickersConfig.Instance.Printer_PageWidth,
                                    (int) MakaoStickersConfig.Instance.Printer_PageHeight
                                   );
            CalcGraphics = Graphics.FromImage(CalcBitmap);

            try
            {
                var cvt = new FontConverter();
                printFontBase = cvt.ConvertFromString(MakaoStickersConfig.Instance.Printer_BaseFont) as Font;
                printFontBold = cvt.ConvertFromString(MakaoStickersConfig.Instance.Printer_BoldFont) as Font;
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error("Ошибка конфертации шрифтов: " + ex.Message + ". Используются шрифты по умолчанию.");
                printFontBase = new Font("Arial", 7);
                printFontBold = new Font("Arial", 8, FontStyle.Bold);  
            }
        }

//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************
        #endregion


        #region TEMPLATEINTERNALS
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        //************************************************************************************************************
        // формирование шаблона стикера
        public string GenerateStickerTemplate(V4.Data.Orders.IOrderItemProduct product,
                                              Dictionary<string, string> NomDictionary,
                                              Dictionary<string, List<string>> TTKDictionary, DateTime OpenOrderTIme)
        {
            return String.Format("{0}\n" +                              // Изготовитель: 
                                 "{1}\n" +                              // Адрес изготовителя: 
                                 "{2}\n" +                              // название товара
                                 "Состав: {3}\n" +                      // состав
                                 "Пищевая ценность в 100г: {4}\n" +
                                 "{5} {6}\n" +   //Дата/время приготов.: 
                                 "Срок годности: {7}\n" +
                                 "Температура хранения: {8}",
                                 MakaoStickersConfig.Instance.Restaurant_Department,
                                 MakaoStickersConfig.Instance.Restaurant_Address,
                                 GetNameProduct(product.Product.Name),
                                 GetProductСomposition(product.Product.Number, NomDictionary, TTKDictionary).ToLower(),
                                 GetProductEnergy(product),
                                 OpenOrderTIme.ToShortDateString(),//DateTime.Now.ToShortDateString(),
                                 OpenOrderTIme.ToShortTimeString(),//DateTime.Now.ToShortTimeString(),
                                 MakaoStickersConfig.Instance.Restaurant_ExpirationTime,
                                 MakaoStickersConfig.Instance.Restaurant_StorageTemperature
                                );
        }

        private string GetNameProduct(string name)
        {
            return name.Replace(Environment.NewLine, String.Empty)
                        .Replace("\n", String.Empty)
                        .Replace("\r", String.Empty)
                        .ToUpper();
        }

        //************************************************************************************************************
        // состав блюда, одной строкой
        private string GetProductСomposition(string DishId,
                                             Dictionary<string, string> NomDictionary,
                                             Dictionary<string, List<string>> TTKDictionary
                                            )
        {
            string res = "";
            try
            {
                if (!TTKDictionary.ContainsKey(DishId))
                    return "";

                foreach (var ProductId in TTKDictionary[DishId])
                {
                    if (NomDictionary.Keys.Contains(ProductId))
                        res += NomDictionary[ProductId] + ", ";
                }
                return res.Substring(0, res.Length - 2);
            }
            catch (Exception)
            {
                return res;
            }
        }

        //************************************************************************************************************
        // энергетическая ценность, одной строкой
        private string GetProductEnergy(V4.Data.Orders.IOrderItemProduct product)
        {
            return String.Format("угл.- {0}г, бел.- {1}г, жир.- {2}г. Энер. ценн.- {3}кал /{4}кДж.",
                                 Math.Round(product.Product.FoodValueCarbohydrate, 2),
                                 Math.Round(product.Product.FoodValueProtein, 2),
                                 Math.Round(product.Product.FoodValueFat, 2),
                                 Math.Round(product.Product.FoodValueCaloricity, 2),
                                 Math.Round(((double)product.Product.FoodValueCaloricity * 4.186), 2)
                                );
        }

//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************
        #endregion


        #region PARSEINTERNALS
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        //************************************************************************************************************
        // парсинг шаблона наклейки в глобальный список стринг-юнитов
        private void ParseTemplate(string StickerTemplate)
        {
            // пересоздаем список
            StringsUnitsList = new List<StickersStringUnit>();
            
            // стартовые значения координат печати
            int CurrentPageNumber = 0;
            float CurrentX = MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth;
            float CurrentY = MakaoStickersConfig.Instance.Printer_VerticalBordersHeight;

            // режем шаблон на базовые информационные поля
            string [] InfoFields = StickerTemplate.Split('\n');

            // проходимся по полям, обрабатываем каждое отдельно
            for (int i = 0; i < InfoFields.Length; i++)
            {

                // проверяем не нужно ли выделять все поле целиком
                bool Printed = false;
                for (int j = 0; j < MakaoStickersConfig.Instance.BoldSubstrings.Count; j++)
                {
                    if (MakaoStickersConfig.Instance.BoldSubstrings[j].BSS == i.ToString())
                    {
                        ParseInfoField_Part(InfoFields[i],
                                            printFontBold,
                                            ref CurrentPageNumber,
                                            ref CurrentX,
                                            ref CurrentY);

                        // здесь гарантированный переход на новую строку
                        // проверяем переход по вертикали. Ибо теперь это имеет смысл
                        CurrentY += CalcGraphics.MeasureString("temp", printFontBase).Height;
                        if (CurrentY > MakaoStickersConfig.Instance.Printer_PageHeight - MakaoStickersConfig.Instance.Printer_VerticalBordersHeight * 2)
                        {
                            CurrentPageNumber++;
                            CurrentY = MakaoStickersConfig.Instance.Printer_VerticalBordersHeight;
                        }

                        // горизонталь гарантированно сбрасывается
                        CurrentX = MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth;

                        Printed = true;
                        break;
                    }
                }

                // если все поле не выделилось - ищем отдельные подстроки, режем поле на куски
                if (!Printed)
                    ParseInfoField(InfoFields[i],
                                   ref CurrentPageNumber,
                                   ref CurrentX,
                                   ref CurrentY);
            }
        }

        //************************************************************************************************************
        // парсинг отдельного информационного поля в глобальный список стринг-юнитов.
        // отличие от ParseTemplate - поступает отдельная строка, без переносов
        private void ParseInfoField(string InfoField,               // базовая строка, полное информационное поле
                                    ref int _CurrentPageNumber,     // номер текущей страницы
                                    ref float _CurrentX,            // координата X на текущей странице
                                    ref float _CurrentY             // координата Y на текущей странице
                                   )
        {
            string tempInfoField = InfoField;
           
            while (tempInfoField.Length > 0)
            {
                // ищем вхождение первой жирной подстроки
                string FirstBoldSubString = GetFirstBoldSubstring(tempInfoField);
                int FirstBoldSubString_Index = tempInfoField.ToUpper().IndexOf(FirstBoldSubString.ToUpper());

                // ничего не нашли
                if (FirstBoldSubString == "")
                {
                    // выбрасываем в парс весь кусок на стандартном шрифте
                    ParseInfoField_Part(tempInfoField, 
                                        printFontBase, 
                                        ref _CurrentPageNumber,
                                        ref _CurrentX,
                                        ref _CurrentY
                                       );
                    // обнуляем исходную строку, все распарсили
                    tempInfoField = "";
                }
                else
                {
                    // выбрасываем в парс фрагмент до первой жирной подстроки на стандартном шрифте
                    if (FirstBoldSubString_Index > 0)
                        ParseInfoField_Part(tempInfoField.Substring(0, FirstBoldSubString_Index), 
                                            printFontBase,
                                            ref _CurrentPageNumber,
                                            ref _CurrentX,
                                            ref _CurrentY
                                           );  

                    // выбрасываем в парс саму жирную подстроку на жирном шрифте
                    ParseInfoField_Part(tempInfoField.Substring(FirstBoldSubString_Index, FirstBoldSubString.Length), 
                                        printFontBold, 
                                        ref _CurrentPageNumber,
                                        ref _CurrentX,
                                        ref _CurrentY
                                       );  

                    // обрезаем текущую строку - выбрасываем обе части
                    tempInfoField = tempInfoField.Substring(FirstBoldSubString_Index + FirstBoldSubString.Length);
                }
            }

            // здесь гарантированный переход на новую строку
            // проверяем переход по вертикали. Ибо теперь это имеет смысл. Но!!
            _CurrentY += CalcGraphics.MeasureString("temp", printFontBase).Height;
            if (_CurrentY > MakaoStickersConfig.Instance.Printer_PageHeight - MakaoStickersConfig.Instance.Printer_VerticalBordersHeight * 2)
            {
                _CurrentPageNumber++;
                _CurrentY = MakaoStickersConfig.Instance.Printer_VerticalBordersHeight;
            }

            // горизонталь гарантированно сбрасывается
            _CurrentX = MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth;
        }

        //************************************************************************************************************
        // парсинг отдельной части информационного поля
        private void ParseInfoField_Part(string InfoField_Part,          // базовая строка, актуальная часть информационного поля
                                         Font Font,                      // шрифт
                                         ref int _CurrentPageNumber,     // номер текущей страницы
                                         ref float _CurrentX,            // координата X на текущей странице
                                         ref float _CurrentY             // координата Y на текущей странице
                                        )
        {
            // во временную строку
            string tempInfoField_Part = InfoField_Part;

            // пока не исчерпается строка
            while (tempInfoField_Part != "")
            {
                // получаем часть, помещающуюся в остатке ширины строки
                string AvalPart = GetAvalStringLength(tempInfoField_Part, 
                                                      Font,
                                                      MakaoStickersConfig.Instance.Printer_PageWidth - MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth - _CurrentX);

                // добавляем доступную строку в глобальный список
                StickersStringUnit SU = new StickersStringUnit(AvalPart, 
                                                               _CurrentPageNumber, 
                                                               Font, 
                                                               _CurrentX,             // условность, выравнивание
                                                               _CurrentY,             // условность, выравнивание
                                                               CalcGraphics.MeasureString(AvalPart, Font).Width,
                                                               CalcGraphics.MeasureString(AvalPart, Font).Height
                                                              );
                StringsUnitsList.Add(SU);
               
                // проверяем переход по горизонтали
                _CurrentX += SU.Width;

                if (Font == printFontBold)
                    _CurrentX -= 4;

                if (_CurrentX > MakaoStickersConfig.Instance.Printer_PageWidth - MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth * 2)
                {
                    _CurrentX = MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth;

                    // проверяем переход по вертикали. В том числе переход к новой странице
                    _CurrentY += CalcGraphics.MeasureString("temp", printFontBase).Height;
                    if (_CurrentY > MakaoStickersConfig.Instance.Printer_PageHeight - MakaoStickersConfig.Instance.Printer_VerticalBordersHeight * 2)
                    {
                        _CurrentPageNumber++;
                        _CurrentY = MakaoStickersConfig.Instance.Printer_VerticalBordersHeight;
                    }
                }

                // обрезаем строку на длинну чтолько что влепленного куска
                tempInfoField_Part = tempInfoField_Part.Substring(AvalPart.Length);
                
            }
        }

        //************************************************************************************************************
        // возвращает значение жирной подстроки из списка жирных строк конфига, первой входящей в строку Str
        private string GetFirstBoldSubstring(string Str)
        {
            int MinIndex = Str.Length + 1;
            string res = "";

            for (int i = 0; i < MakaoStickersConfig.Instance.BoldSubstrings.Count; i++)
            {
                if (
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "0") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "1") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "2") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "3") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "4") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "5") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "6") &&
                    (MakaoStickersConfig.Instance.BoldSubstrings[i].BSS != "7")
                   )
                {

                    int Index = Str.ToUpper().IndexOf(MakaoStickersConfig.Instance.BoldSubstrings[i].BSS.ToUpper());
                    if ((Index >= 0) && (Index < MinIndex))
                    {
                        MinIndex = Index;
                        res = MakaoStickersConfig.Instance.BoldSubstrings[i].BSS;
                    }
                }
            }

            return res;
        }

        //************************************************************************************************************
        // возвращает подстроку из строки Str, помещающуюся в пространстве AvalWidth
        private string GetAvalStringLength(string Str,               // исходная строка
                                           Font F,                   // актуальный шрифт
                                           float AvalWidth           // доступное пространство
                                          )
        {
            string res = "";

            for (int i = 0; i < Str.Length; i++)
            {
                if (CalcGraphics.MeasureString(res, F).Width <= AvalWidth)
                    res += Str[i];
            }

            return res;
        }

//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************
        #endregion


        #region PRINTINTERNALS
//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************

        //************************************************************************************************************
        // глобальная печать стикера
        public bool PrintStiker(string StickerTemplate)
        {
            bool res = true;
            try
            {
                // парсим шаблон, сбрасываем глобальные курсоры
                ParseTemplate(StickerTemplate);
                CurrentSUIndex = 0;

                PrintDocument pd = new PrintDocument();
                pd.PrintPage += new PrintPageEventHandler(this.PrintPage);
                if (MakaoStickersConfig.Instance.Printer_Name.Trim() != "")
                    pd.PrinterSettings.PrinterName = MakaoStickersConfig.Instance.Printer_Name;
                pd.Print();
            }
            catch (Exception ex)
            {
                res = false;
            }

            return res;
        }



        //************************************************************************************************************
        // печать страницы стикера
        private void PrintPage(object sender, PrintPageEventArgs ev)
        {
            // засекаем номер текущей страницы
            int CurrentPageNum = StringsUnitsList[CurrentSUIndex].PageNumber;

            while (
                   (CurrentSUIndex < StringsUnitsList.Count) && 
                   (StringsUnitsList[CurrentSUIndex].PageNumber == CurrentPageNum)
                  )
            {
                // печатаем текущий юнит
                if (StringsUnitsList[CurrentSUIndex].Font != printFontBold)
                    ev.Graphics.DrawString(StringsUnitsList[CurrentSUIndex].Value,
                                           StringsUnitsList[CurrentSUIndex].Font,
                                           Brushes.Black,
                                           StringsUnitsList[CurrentSUIndex].X,
                                           StringsUnitsList[CurrentSUIndex].Y,
                                           new StringFormat()
                                          );
                // поправки для шрифтов - здесь. Дабы парс оставался скалярно чистым!!!
                else
                {
                    if (StringsUnitsList[CurrentSUIndex].X <= MakaoStickersConfig.Instance.Printer_HorisontalBordersWidth)
                        ev.Graphics.DrawString(StringsUnitsList[CurrentSUIndex].Value,
                                               StringsUnitsList[CurrentSUIndex].Font,
                                               Brushes.Black,
                                               StringsUnitsList[CurrentSUIndex].X,
                                               StringsUnitsList[CurrentSUIndex].Y-1,
                                               new StringFormat()
                                              );
                    else
                        ev.Graphics.DrawString(StringsUnitsList[CurrentSUIndex].Value,
                                               StringsUnitsList[CurrentSUIndex].Font,
                                               Brushes.Black,
                                               StringsUnitsList[CurrentSUIndex].X - 2,
                                               StringsUnitsList[CurrentSUIndex].Y - 1,
                                               new StringFormat()
                                              );
                }

                // переходим к следующему юниту
                CurrentSUIndex++;
            }

            // переходим к следующей странице если список еще не исчерпался
            if (CurrentSUIndex < StringsUnitsList.Count)
                ev.HasMorePages = true;
            else
                ev.HasMorePages = false;
        }


//************************************************************************************************************
//************************************************************************************************************
//************************************************************************************************************
        #endregion

    }


    // Класс, отвечающий за единицу строки на странице наклейки
    class StickersStringUnit
    {
        public string Value;        // значение

        public int PageNumber;      // номер страницы. На случай многостраничной наклейки
        public Font Font;           // шрифт
        
        public float X;             // прямоугольник
        public float Y;
        public float Height;
        public float Width;

        //************************************************************************************************************
        public StickersStringUnit(string _Value,
                                  int _PageNumber,
                                  Font _F,
                                  float _X,
                                  float _Y,
                                  float _Width,
                                  float _Heigth
                                 )
        {
            this.Value = _Value;
            this.PageNumber = _PageNumber;
            this.Font = _F;
            this.X = _X;
            this.Y = _Y;
            this.Width = _Width;
            this.Height = _Heigth;
        }

    }
}
