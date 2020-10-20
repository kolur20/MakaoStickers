using System;

using System.Collections.Generic;
using JetBrains.Annotations;
using Resto.Framework.Common.Print.Tags.Xml;
using Resto.Front.Api.V4;
using Resto.Front.Api.V4.Data.Cheques;              // debug!!
using Resto.Front.Api.V4.Data.Orders;     
using Resto.Front.Api.V4.Extensions;
using System.IO;
using Resto.Front.Api.MakaoStickers.Settings;
using System.Windows;

namespace Resto.Front.Api.MakaoStickers
{
    internal sealed class BillChequeExtender : IDisposable
    {
        [NotNull]
        private readonly IDisposable subscription;

        public Dictionary<string, string> NomDictionary;
        public Dictionary<string, List<string>> TTKDictionary;

        // печатающая сущность
        MakaoStickersPrinter Printer;
        //доп проверка на задвоение
        Dictionary<long, List<IOrderItemProduct>> listOrderPrinted;

        //************************************************************************************************************
        // конструктор
        internal BillChequeExtender(Dictionary<string, string> _NomDictionary,
                                    Dictionary<string, List<string>> _TTKDictionary)
        {
            this.NomDictionary = _NomDictionary;
            this.TTKDictionary = _TTKDictionary;

            Printer = new MakaoStickersPrinter();

            // СТАРАЯ ВЕРСИЯ
            //subscription = PluginContext.Notifications.SubscribeOnBillChequePrinting(MainPrintStikers);
            // НОВАЯ ВЕРСИЯ
            //subscription = PluginContext.Notifications.OrderChanged.Subscribe(MainOrderChanged);
            // ИЗМЕНЕННАЯ ВЕРСИЯ
            subscription = PluginContext.Notifications.OrderChanged.Subscribe(OrderChang);

            listOrderPrinted = new Dictionary<long, List<IOrderItemProduct>>();
        }


        //Измененная версия с реакцией на добавление и печать 6.2.1126.2
        //************************************************************************************************************
        [NotNull]
        public void OrderChang([NotNull] IOrder order)
        {
            //order is close?
            if (order.Status == OrderStatus.Closed)
            { return; }
            if (order.Status == OrderStatus.Bill || order.Status == OrderStatus.Deleted)
            {
                if (listOrderPrinted.ContainsKey(order.Number))
                {
                    listOrderPrinted.Remove(order.Number);
                    PluginContext.Log.Info($"Очередь печати для заказ № {order.Number.ToString()} очищена. Заказ закрыт или удален.");
                }
                return;
            }
            if (!TablIsNeedPrinted(order)) return;
            bool PrintIsNeed = false;
            List<IOrderItemProduct> products = null;
            try
            {
                //получили весь перечень заказа
                products = PluginContext.Operations.GetProductsByOrder(order);
                products = products.FindAll(product => product.PrintTime != null);
                if (products.Count != 0)
                {
                    //все что отпечатано, добавить в список
                    if (!listOrderPrinted.ContainsKey(order.Number))
                    {
                        listOrderPrinted.Add(order.Number, new List<IOrderItemProduct>(products));
                        
                    }
                    else
                    {
                        var oldCountProducts = listOrderPrinted[order.Number].Count;
                        var newCountPorducts = products.Count;
                        if (newCountPorducts > oldCountProducts)
                        {
                            //удаляем все позици, которое уже были распечатаны
                            var l = listOrderPrinted[order.Number];
                            products.RemoveAll(x => l.Contains(x));
                            listOrderPrinted[order.Number].AddRange(products);
                        }
                        else { return; }
                    }

                    PrintIsNeed = true;
                }
                
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error("Реакция на изменение заказа. Не удалось проверить необходимость печати наклеек заказа " + order.Number.ToString(), ex);
                return;
            }
            if (PrintIsNeed)
            {
                //проверка на доступость позиций

                //PluginContext.Log.Info($"Количество позиций в заказе: {products.Count}");

                //после этого в products остается только то, что нужно расспечатать
                //PluginContext.Log.Info($"Количество позиций для печати: {products.Count}");
                // для каждой позиции
                try
                {
                    foreach (var product in products)
                    {
                        //не печатаем модификаторы
                        if (product.Product.Type.ToString().Trim() == Resto.Front.Api.
                                                                        V4.Data.Assortment.
                                                                        ProductType.Modifier.
                                                                        ToString().Trim())
                            continue;
                        //не печатаем удаленны позиции
                        if (product.Deleted)
                            continue;



                        // формируем файл. Именно здесь пишем, чтобы иметь имя и удалить после печати всех копий
                        PluginContext.Log.Info("Формирование шаблона наклейки товара " + product.Product.Name.Trim());
                        string StickerText = Printer.GenerateStickerTemplate(product, NomDictionary, TTKDictionary, order.OpenTime);
                        if (StickerText != "")
                            PluginContext.Log.Info("Шаблон наклейки товара " + product.Product.Name.Trim().Replace("\n", String.Empty) + " +  успешно сформирован.");
                        else
                        {
                            PluginContext.Log.Error("Не удалось сформировать шаблон наклейки товара " + product.Product.Name.Trim());
                            continue;
                        }

                        // печатаем файл для каждой единицы позиции
                        for (int i = 0; i < product.Amount; i++)
                        {
                            if (Printer.PrintStiker(StickerText))
                            {
                                PluginContext.Log.Info("Отправлена на печать " + (i + 1).ToString() + "-я копия наклейки товара " + product.Product.Name.Trim());
                            }
                            else
                            {
                                PluginContext.Log.Error("Не удалось распечатать " + (i + 1).ToString() + "-ю копию наклейки товара " + product.Product.Name.Trim());
                            }
                        }

                    }

                    PluginContext.Log.Info("Печать наклеек заказа " + order.Number.ToString() + " завершена.");
                }
                catch (Exception ex)
                {
                    PluginContext.Log.Error($"Ошибка: {ex.Message}");
                }
            }
        }
        
        private bool TablIsNeedPrinted (IOrder order)
        {
            // определяем все ли в порядке со столом, нужно для этого стола что-либо печатать в стикеры
            if (
                (order.Tables[0].Number >= MakaoStickersConfig.Instance.Restaurant_SpecialHall_StartTableInd) &&
                (order.Tables[0].Number <= MakaoStickersConfig.Instance.Restaurant_SpecialHall_EndTableInd)
               )
            {
                PluginContext.Log.Info($"Печать наклеек для данного заказа подтвержается: номер стола попадает в заданный диапазон: {order.Tables[0].Number}");
                return true;
            }
            else
            {
                PluginContext.Log.Warn($"Печать наклеек для данного заказа не нужна: номер стола не попадает в заданный диапазон: {order.Tables[0].Number}");
                return false;
            }
        }

        // НОВАЯ ВЕРСИЯ, ЗАТОЧЕННАЯ НА РЕАКЦИЮ НА ИЗМЕНЕНИЕ ЗАКАЗА, 6.2.1126.0
        //************************************************************************************************************
        /*
        [NotNull]
        public void MainOrderChanged([NotNull] IOrder inEvent)
        {
            //если заказ закрылся очистить стек заказов
            if (inEvent.Status == OrderStatus.Closed)
            {
                listOrderPrinted.Remove(inEvent.Number);
                return;
            }
            // проверяем действительно ли есть повод для печати
            bool PrintIsNeed = false;
           
            try
            {
                List<V4.Data.Orders.IOrderItemProduct> products = PluginContext.Operations.GetProductsByOrder(inEvent);
                for (int i = 0; i < products.Count; i++)
                {
                    // если у какой-либо позиции время печати не пусто и печать была не слишком давно 
                    PrintIsNeed = (
                                   (products[i].PrintTime != null) &&
                                   (products[i].PrintTime.ToString() != "") 
                                   //&& (DateTime.Now.Subtract(products[i].PrintTime.Value).TotalSeconds <= MakaoStickersConfig.Instance.Plugin_PrintOrderDelta)
                                  );
                    if (PrintIsNeed)
                        break;  
                } 
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error("Реакция на изменение заказа. Не удалось проверить необходимость печати наклеек заказа " + inEvent.Number.ToString(), ex);
                return;
            }

            // продолжаем только если есть необходимость печати
            if (PrintIsNeed)
            {
                PluginContext.Log.Info("Инициирована печать наклеек заказа " + inEvent.Number.ToString());

                // определяем все ли в порядке со столом, нужно для этого стола что-либо печатать в стикеры
                if (
                    (inEvent.Tables[0].Number >= MakaoStickersConfig.Instance.Restaurant_SpecialHall_StartTableInd) &&
                    (inEvent.Tables[0].Number <= MakaoStickersConfig.Instance.Restaurant_SpecialHall_EndTableInd)
                   )
                {
                    PluginContext.Log.Info("Печать наклеек для данного заказа подтвержается: номер стол попадает в заданный диапазон.");
                }
                else
                {
                    PluginContext.Log.Warn("Печать наклеек для данного заказа не нужна: номер стол не попадает в заданный диапазон.");
                    return;
                }


                // вытаскиваем список позиций заказа из контекста фронта
                PluginContext.Log.Info("Получение списка позиций заказа " + inEvent.ToString());
                List<V4.Data.Orders.IOrderItemProduct> products = null;
                try
                {
                    products = PluginContext.Operations.GetProductsByOrder(inEvent);
                    PluginContext.Log.Info("Количество позиций: " + products.Count);
                }
                catch (Exception ex)
                {
                    PluginContext.Log.Error("Не удалось получить список позиций заказа " + inEvent.ToString(), ex);
                    return;
                }

                //проверка на то, что в данном заказе уже была печать
                if (!listOrderPrinted.ContainsKey(inEvent.Number))
                {
                    listOrderPrinted.Add(inEvent.Number, new List<IOrderItemProduct>(products));
                }
                else
                {
                    var oldCountProducts = listOrderPrinted[inEvent.Number].Count;
                    var newCountPorducts = products.Count;
                    if (newCountPorducts > oldCountProducts)
                    {
                        //удаляем все позици, которое уже были распечатаны
                        var l = listOrderPrinted[inEvent.Number];
                        products.RemoveAll(x => l.Contains(x));
                    }
                    else { return; }
                }
                // для каждой позиции
                foreach (var product in products)
                {
                    if (product.Product.Type.ToString().Trim() == Resto.Front.Api.
                                                                    V4.Data.Assortment.
                                                                    ProductType.Modifier.
                                                                    ToString().Trim())
                        continue;

                    // если нужно печатать
                    if (
                        (product.PrintTime != null) &&
                        (product.PrintTime.ToString() != "") &&
                        (DateTime.Now.Subtract(product.PrintTime.Value).TotalSeconds <= MakaoStickersConfig.Instance.Plugin_PrintOrderDelta)
                       )
                    {
                        // формируем файл. Именно здесь пишем, чтобы иметь имя и удалить после печати всех копий
                        PluginContext.Log.Info("Формирование шаблона наклейки товара " + product.Product.Number.ToString());
                        string StickerText = Printer.GenerateStickerTemplate(product, NomDictionary, TTKDictionary);
                        if (StickerText != "")
                            PluginContext.Log.Info("Шаблон наклейки товара " + product.Product.Number.ToString() + " успешно сформирован.");
                        else
                        {
                            PluginContext.Log.Error("Не удалось сформировать шаблон наклейки товара " + product.Product.Number.ToString());
                            continue;
                        }

                        // печатаем файл для каждой единицы позиции
                        for (int i = 0; i < product.Amount; i++)
                        {
                            if (Printer.PrintStiker(StickerText))
                            {
                                PluginContext.Log.Info("Отправлена на печать " + i.ToString() + "-я копия наклейки товара " + product.Product.Number.ToString());
                            }
                            else
                            {
                                PluginContext.Log.Error("Не удалось распечатать " + i.ToString() + "-ю копию наклейки товара " + product.Product.Number.ToString());
                            }
                        }
                    }
                }

                PluginContext.Log.Info("Печать наклеек заказа " + inEvent.Number.ToString() + " завершена.");
            }
        }
        */


        // СТАРАЯ СТАБИЛЬНАЯ ВЕРСИЯ, ЗАТОЧЕННАЯ СТРОГО НА ПЕЧАТЬ ПРЕЧЕКА!!
        /*
        // основной метод подписки на печать пречека
        [NotNull]
        private BillCheque MainPrintStikers(Guid orderId)
        {
            var currOrder = PluginContext.Operations.GetOrderById(orderId);

            currOrder.Customers[0].

            PluginContext.Log.Info("Инициирована печать наклеек заказа " + currOrder.Number.ToString());

            // определяем все ли в порядке со столом
            if (
                (currOrder.Tables[0].Number >= MakaoStickersConfig.Instance.Restaurant_SpecialHall_StartTableInd) &&
                (currOrder.Tables[0].Number <= MakaoStickersConfig.Instance.Restaurant_SpecialHall_EndTableInd)
               )
            {
                PluginContext.Log.Info("Печать наклеек для данного заказа подтвержается: номер стол попадает в заданный диапазон.");
            }
            else
            {
                PluginContext.Log.Warn("Печать наклеек для данного заказа не нужна: номер стол не попадает в заданный диапазон.");
                return null;
            }

            // вытаскиваем список позиций заказа из контекста фронта
            PluginContext.Log.Info("Получение списка позиций заказа " + orderId.ToString());
            List<V4.Data.Orders.IOrderItemProduct> products = null;
            try
            {
                products = PluginContext.Operations.GetProductsByOrder(currOrder);
                PluginContext.Log.Info("Количество позиций: " + products.Count);
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error("Не удалось получить список позиций заказа " + orderId.ToString(), ex);
                return null;
            }

            // для каждой позиции
            foreach (var product in products)
            {
                // формируем файл. Именно здесь пишем, чтобы иметь имя и удалить после печати всех копий
                PluginContext.Log.Info("Формирование шаблона наклейки товара " + product.Product.Number.ToString());
                string StickerText = Printer.GenerateStickerTemplate(product, NomDictionary, TTKDictionary);
                if (StickerText != "")
                    PluginContext.Log.Info("Шаблон наклейки товара " + product.Product.Number.ToString() +" успешно сформирован.");
                else
                {
                    PluginContext.Log.Error("Не удалось сформировать шаблон наклейки товара " + product.Product.Number.ToString());
                    continue;
                }

                // печатаем файл для каждой единицы позиции
                for (int i = 0; i < product.Amount; i++)
                {
                    if (Printer.PrintStiker(StickerText))
                    {
                        PluginContext.Log.Info("Отправлена на печать " + i.ToString() + "-я копия наклейки товара " + product.Product.Number.ToString());
                    }
                    else
                    {
                        PluginContext.Log.Error("Не удалось распечатать " + i.ToString() + "-ю копию наклейки товара " + product.Product.Number.ToString());
                    }
                }
            }

            PluginContext.Log.Info("Печать наклеек заказа " + orderId.ToString() + " завершена.");
            
            return null;
        }
        
        */


        //************************************************************************************************************
        public void Dispose()
        {
            subscription.Dispose();
        }

    }
}
