﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;

namespace Nevoweb.DNN.NBrightBuy.Providers.PromoProvider
{
    public static class PromoUtils
    {
        #region "Group promo"

        public static string CalcGroupPromo(int portalId)
        {
            var objCtrl = new NBrightBuyController();
            var l = objCtrl.GetList(portalId, -1, "CATEGORYPROMO", "", " [XMLData].value('(genxml/dropdownlist/processorder)[1]','int') ", 0, 0, 0, 0, Utils.GetCurrentCulture());
            foreach (var p in l)
            {
                CalcGroupPromoItem(p);
            }
            return "OK";
        }

        public static string CalcGroupPromoItem(NBrightInfo p)
        {
            var objCtrl = new NBrightBuyController();
            var typeselect = p.GetXmlProperty("genxml/radiobuttonlist/typeselect");
            var catgroupid = p.GetXmlProperty("genxml/dropdownlist/catgroupid");
            var propgroupid = p.GetXmlProperty("genxml/dropdownlist/propgroupid");
            var promoname = p.GetXmlProperty("genxml/textbox/name");
            var amounttype = p.GetXmlProperty("genxml/radiobuttonlist/amounttype");
            var amount = p.GetXmlPropertyDouble("genxml/textbox/amount");
            var validfrom = p.GetXmlProperty("genxml/textbox/validfrom");
            var validuntil = p.GetXmlProperty("genxml/textbox/validuntil");
            var overwrite = p.GetXmlPropertyBool("genxml/checkbox/overwrite");
            var disabled = p.GetXmlPropertyBool("genxml/checkbox/disabled");
            var lastcalculated = p.GetXmlProperty("genxml/hidden/lastcalculated");
            var whichprice = p.GetXmlPropertyInt("genxml/radiobuttonlist/whichprice");
            var runfreq = p.GetXmlPropertyInt("genxml/radiobuttonlist/runfreq");

            if (!disabled)
            {
                var runcalc = true;
                if (runfreq == 1 || runfreq == 3) // run freq is date range
                {
                    if (runfreq == 3)
                    {
                        // run constantly, set end date to +100 year
                        validfrom = DateTime.Now.AddYears(-1).ToString("s");
                        validuntil = DateTime.Now.AddYears(100).ToString("s");
                    }
                    // run date range
                    if (Utils.IsDate(lastcalculated))
                    {
                        if (Convert.ToDateTime(lastcalculated) >= p.ModifiedDate) runcalc = false; // only run if changed 
                        if (DateTime.Now.Date > Convert.ToDateTime(lastcalculated).Date.AddDays(1)) runcalc = true; // every day just after midnight. (for site performace) 
                    }
                    if (Utils.IsDate(validuntil))
                    {
                        if (DateTime.Now.Date > Convert.ToDateTime(validuntil)) runcalc = true; // need to disable the promo if passed date
                    }
                    if ((runcalc) && Utils.IsDate(validfrom) && Utils.IsDate(validuntil))
                    {
                        var dteF = Convert.ToDateTime(validfrom).Date;
                        var dteU = Convert.ToDateTime(validuntil).Date;
                        CategoryData gCat;
                        var groupid = catgroupid;
                        if (typeselect != "cat") groupid = propgroupid;

                        gCat = CategoryUtils.GetCategoryData(groupid, Utils.GetCurrentCulture());
                        var prdList = gCat.GetAllArticles();

                        foreach (var prd in prdList)
                        {
                            if (DateTime.Now.Date >= dteF && DateTime.Now.Date <= dteU)
                            {
                                // CALC Promo
                                CalcProductSalePrice(p.PortalId, prd.ParentItemId, amounttype, amount, promoname, p.ItemID, whichprice, overwrite, dteF, dteU);
                            }
                            if (DateTime.Now.Date > dteU)
                            {
                                // END Promo
                                RemoveProductPromoData(p.PortalId, prd.ParentItemId, p.ItemID, whichprice);
                            }
                            ProductUtils.RemoveProductDataCache(p.PortalId, prd.ParentItemId);
                        }
                        if (DateTime.Now.Date > dteU)
                        {
                            // END Promo
                            p.SetXmlProperty("genxml/checkbox/disabled", "True");
                        }

                    }
                }
                if (runfreq == 2) // run Once, do update and disable promo.
                {                    
                    CategoryData gCat;
                    var groupid = catgroupid;
                    if (typeselect != "cat") groupid = propgroupid;

                    gCat = CategoryUtils.GetCategoryData(groupid, Utils.GetCurrentCulture());
                    var prdList = gCat.GetAllArticles();

                    foreach (var prd in prdList)
                    {
                            // CALC Promo
                        CalcProductSalePrice(p.PortalId, prd.ParentItemId, amounttype, amount, promoname, p.ItemID, whichprice, overwrite, DateTime.Now, DateTime.Now);
                        ProductUtils.RemoveProductDataCache(p.PortalId, prd.ParentItemId);
                    }
                    p.SetXmlProperty("genxml/checkbox/disabled", "True");
                }
                if (runfreq == 4) // remove
                {
                    var groupid = catgroupid;
                    if (typeselect != "cat") groupid = propgroupid;

                    var gCat = CategoryUtils.GetCategoryData(groupid, Utils.GetCurrentCulture());
                    var prdList = gCat.GetAllArticles();

                    foreach (var prd in prdList)
                    {
                        RemoveProductPromoData(p.PortalId, prd.ParentItemId, p.ItemID, whichprice);
                        ProductUtils.RemoveProductDataCache(p.PortalId, prd.ParentItemId);
                    }

                    p.SetXmlProperty("genxml/checkbox/disabled", "True");
                }
                p.SetXmlProperty("genxml/hidden/lastcalculated", DateTime.Now.AddSeconds(10).ToString("O")); // Add 10 sec to time so we don't get exact clash with update time.
                objCtrl.Update(p);
            }
            return "OK";
        }

        public static string RemoveGroupProductPromo(int portalId,int promoid)
        {
            var objCtrl = new NBrightBuyController();
            var p = objCtrl.GetData(promoid);

            var typeselect = p.GetXmlProperty("genxml/radiobuttonlist/typeselect");
            var catgroupid = p.GetXmlProperty("genxml/dropdownlist/catgroupid");
            var propgroupid = p.GetXmlProperty("genxml/dropdownlist/propgroupid");

            CategoryData gCat;
            var groupid = catgroupid;
            if (typeselect != "cat") groupid = propgroupid;

            gCat = CategoryUtils.GetCategoryData(groupid, Utils.GetCurrentCulture());
            if (gCat.Exists)
            {
                var prdList = gCat.GetAllArticles();

                foreach (var prd in prdList)
                {
                    // END Promo
                    RemoveProductPromoData(portalId, prd.ParentItemId, p.ItemID);
                    ProductUtils.RemoveProductDataCache(prd.PortalId, prd.ParentItemId);
                }
            }
            return "OK";
        }

        private static void EndProductSalePrice(int portalId,int productId, int promoid)
        {
            var objCtrl = new NBrightBuyController();
            var prdData = objCtrl.GetData(productId);

            var nodList = prdData.XMLDoc.SelectNodes("genxml/models/genxml");
            if (nodList != null)
            {
                var currentpromoid = prdData.GetXmlPropertyInt("genxml/hidden/promoid");
                if (currentpromoid == promoid)
                {
                    var lp = 1;
                    foreach (XmlNode nod in nodList)
                    {
                        prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", 0);
                        lp += 1;
                    }
                    objCtrl.Update(prdData);
                    RemoveProductPromoData(portalId, productId, promoid);
                }
            }
        }

        private static void CalcProductSalePrice(int portalid, int productId, string amounttype, double amount, string promoname, int promoid,int whichprice , bool overwrite,DateTime dteF, DateTime dteU)
        {
            var cultureList = DnnUtils.GetCultureCodeList(portalid);
            var objCtrl = new NBrightBuyController();
            var prdData = objCtrl.GetData(productId);

            var nodList = prdData.XMLDoc.SelectNodes("genxml/models/genxml");
            if (nodList != null)
            {
                var currentpromoid = prdData.GetXmlPropertyInt("genxml/hidden/promoid");
                if (currentpromoid == 0 || currentpromoid == promoid || overwrite)
                {
                    prdData.SetXmlProperty("genxml/hidden/promotype", "PROMOGROUP");
                    prdData.SetXmlPropertyDouble("genxml/hidden/promoname", promoname);
                    prdData.SetXmlProperty("genxml/hidden/promoid", promoid.ToString());
                    prdData.SetXmlProperty("genxml/hidden/promocalcdate", DateTime.Now.ToString("O"));
                    prdData.SetXmlProperty("genxml/hidden/datefrom", dteF.ToString("O"));
                    prdData.SetXmlProperty("genxml/hidden/dateuntil", dteU.ToString("O"));

                    var lp = 1;
                    foreach (XmlNode nod in nodList)
                    {
                        var nbi = new NBrightInfo();
                        nbi.XMLData = nod.OuterXml;
                        var disablesale = nbi.GetXmlPropertyBool("genxml/checkbox/chkdisablesale");
                        var disabledealer = nbi.GetXmlPropertyBool("genxml/checkbox/chkdisabledealer");
                        var unitcost = nbi.GetXmlPropertyDouble("genxml/textbox/txtunitcost");
                        var dealercost = nbi.GetXmlPropertyDouble("genxml/textbox/txtdealercost");
                        Double newamt = 0;
                        Double newdealersaleamt = 0;
                        Double newdealeramt = 0;
                        if (amounttype == "1")
                        {
                            newamt = unitcost - amount;
                            newdealersaleamt = dealercost - amount;
                        }
                        else
                        {
                            newamt = unitcost - ((unitcost/100)*amount);
                            newdealersaleamt = dealercost - ((dealercost / 100) * amount);
                        }
                        newdealeramt = newamt;
                        if (newamt < 0) newamt = 0;
                        if (newdealersaleamt < 0) newdealersaleamt = 0;

                        var currentprice = prdData.GetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice");

                        if (!overwrite)
                        {
                            if (currentprice == 0) overwrite = true;
                            if (currentpromoid == promoid) overwrite = true;
                        }
                        if (overwrite)
                        {
                            switch (whichprice)
                            {
                                case 1:
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", newamt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid", promoid.ToString());
                                    break;
                                case 2:
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealercost", newdealeramt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promodealercostid", promoid.ToString());
                                    break;
                                case 3:
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealersale", newdealersaleamt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promodealersaleid", promoid.ToString());
                                    break;
                                case 4:
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealersale", newdealersaleamt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promodealersaleid", promoid.ToString());
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", newamt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid", promoid.ToString());
                                    break;
                                default:
                                    prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", newamt);
                                    prdData.SetXmlProperty("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid", promoid.ToString());
                                    break;
                            }
                        }
                        if (disablesale)
                        {
                            prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", 0);
                            prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid");
                        }
                        if (disabledealer)
                        {
                            prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealercost", 0);
                            prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promodealercostid");
                            prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealersale", 0);
                            prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promodealersaleid");
                        }

                        lp += 1;
                    }
                    objCtrl.Update(prdData);

                    foreach (var lang in cultureList)
                    {
                        var p = objCtrl.GetDataLang(promoid, lang);
                        var prdDataLang = objCtrl.GetDataLang(productId, lang);
                        if (prdDataLang != null)
                        {
                            prdDataLang.SetXmlProperty("genxml/hidden/promodesc", p.GetXmlProperty("genxml/textbox/description"));
                            objCtrl.Update(prdDataLang);
                        }
                    }
                }
            }

        }

        #endregion

        #region "Multi-Buy"

        public static string CalcMultiBuyPromo(int portalId)
        {
            var objCtrl = new NBrightBuyController();
            var l = objCtrl.GetList(portalId, -1, "MULTIBUYPROMO", "", "", 0, 0, 0, 0, Utils.GetCurrentCulture());
            foreach (var p in l)
            {
                CalcMultiBuyPromoItem(p);
            }
            return "OK";
        }

        public static string CalcMultiBuyPromoItem(NBrightInfo p)
        {
            var objCtrl = new NBrightBuyController();
            var propgroupid = p.GetXmlPropertyInt("genxml/dropdownlist/propbuy");
            var promoname = p.GetXmlProperty("genxml/textbox/name");
                var validfrom = p.GetXmlProperty("genxml/textbox/validfrom");
                var validuntil = p.GetXmlProperty("genxml/textbox/validuntil");
                var disabled = p.GetXmlPropertyBool("genxml/checkbox/disabled");
                var lastcalculated = p.GetXmlProperty("genxml/hidden/lastcalculated");

            if (!disabled)
            {
                var runcalc = true;
                if (Utils.IsDate(lastcalculated))
                {
                    if (Convert.ToDateTime(lastcalculated) >= p.ModifiedDate) runcalc = false;  // only run if changed 
                    if (DateTime.Now.Date > Convert.ToDateTime(lastcalculated).Date.AddDays(1)) runcalc = true; // every day just after midnight. (for site performace) 
                }
                if (Utils.IsDate(validuntil))
                {
                    if (DateTime.Now.Date > Convert.ToDateTime(validuntil)) runcalc = true; // need to disable the promo if passed date
                }
                if ((runcalc) && Utils.IsDate(validfrom) && Utils.IsDate(validuntil))
                {
                    var dteF = Convert.ToDateTime(validfrom).Date;
                    var dteU = Convert.ToDateTime(validuntil).Date;
                    CategoryData gCat;

                    gCat = CategoryUtils.GetCategoryData(propgroupid, Utils.GetCurrentCulture());
                    var prdList = gCat.GetAllArticles();

                    foreach (var prd in prdList)
                    {
                        if (DateTime.Now.Date >= dteF && DateTime.Now.Date <= dteU)
                        {
                            // CALC Promo
                            FlagProductMultiBuy(p.PortalId, prd.ParentItemId, promoname, p.ItemID, "PROMOMULTIBUY",dteF,dteU);
                        }
                        if (DateTime.Now.Date > dteU)
                        {
                            // END Promo
                            RemoveProductPromoData(p.PortalId, prd.ParentItemId, p.ItemID);
                            p.SetXmlProperty("genxml/checkbox/disabled", "True");
                            objCtrl.Update(p);
                        }
                        ProductUtils.RemoveProductDataCache(p.PortalId, prd.ParentItemId);
                    }

                    p.SetXmlProperty("genxml/hidden/lastcalculated", DateTime.Now.AddSeconds(10).ToString("O")); // Add 10 sec to time so we don't get exact clash with update time.
                    objCtrl.Update(p);
                }
            }
            return "OK";
        }

        private static void FlagProductMultiBuy(int portalid,int productId, string promoname, int promoid,String promoType, DateTime dteF, DateTime dteU)
        {
            var cultureList = DnnUtils.GetCultureCodeList(portalid);
            var objCtrl = new NBrightBuyController();
            var prdData = objCtrl.GetData(productId);

            var nodList = prdData.XMLDoc.SelectNodes("genxml/models/genxml");
            if (nodList != null)
            {

                var currentpromoid = prdData.GetXmlPropertyInt("genxml/hidden/promoid");
                if (currentpromoid == 0 || currentpromoid == promoid)
                {
                    prdData.SetXmlProperty("genxml/hidden/promotype", promoType);
                    prdData.SetXmlProperty("genxml/hidden/promoname", promoname);
                    prdData.SetXmlProperty("genxml/hidden/promoid", promoid.ToString());
                    prdData.SetXmlProperty("genxml/hidden/promocalcdate", DateTime.Now.ToString("O"));
                    prdData.SetXmlProperty("genxml/hidden/datefrom", dteF.ToString("O"));
                    prdData.SetXmlProperty("genxml/hidden/dateuntil", dteU.ToString("O"));

                    objCtrl.Update(prdData);
                    if (promoType == "PROMOMULTIBUY")
                    {
                        foreach (var lang in cultureList)
                        {
                            var p = objCtrl.GetDataLang(promoid, lang);
                            var prdDataLang = objCtrl.GetDataLang(productId, lang);
                            if (prdDataLang != null)
                            {
                                prdDataLang.SetXmlProperty("genxml/hidden/promodesc", p.GetXmlProperty("genxml/textbox/description"));
                                objCtrl.Update(prdDataLang);
                            }
                        }
                    }
                }
            }

        }

        public static string RemoveMultiBuyProductPromo(int portalId, int promoid)
        {
            var objCtrl = new NBrightBuyController();
            var p = objCtrl.GetData(promoid);

            var propgroupid = p.GetXmlPropertyInt("genxml/dropdownlist/propbuy");
            var propapplygroupid = p.GetXmlPropertyInt("genxml/dropdownlist/propapply");

            var gCat = CategoryUtils.GetCategoryData(propgroupid, Utils.GetCurrentCulture());
            if (gCat.Exists)
            {

                var prdList = gCat.GetAllArticles();

                foreach (var prd in prdList)
                {
                    // END Promo
                    RemoveProductPromoData(portalId, prd.ParentItemId, promoid);
                    ProductUtils.RemoveProductDataCache(prd.PortalId, prd.ParentItemId);
                }
            }

            if (propapplygroupid != propgroupid && propapplygroupid > 0)
            {
                gCat = CategoryUtils.GetCategoryData(propapplygroupid, Utils.GetCurrentCulture());
                if (gCat.Exists)
                {
                    var prdList2 = gCat.GetAllArticles();

                    foreach (var prd in prdList2)
                    {
                        // END Promo
                        RemoveProductPromoData(p.PortalId, prd.ParentItemId, p.ItemID);
                        ProductUtils.RemoveProductDataCache(p.PortalId, prd.ParentItemId);
                    }
                }
            }

            return "OK";
        }

        #endregion


        #region "Shared"

        public static void RemoveProductPromoData(int portalid, int productId, int promoid, int whichprice = 1)
        {
            var cultureList = DnnUtils.GetCultureCodeList(portalid);
            var objCtrl = new NBrightBuyController();
            var prdData = objCtrl.GetData(productId);

            var currentpromoid = prdData.GetXmlPropertyInt("genxml/hidden/promoid");
            if (currentpromoid == promoid || currentpromoid == 0) // multiple promo may have been applied and removed.
            {
                prdData.RemoveXmlNode("genxml/hidden/promotype");
                prdData.RemoveXmlNode("genxml/hidden/promoname");
                prdData.RemoveXmlNode("genxml/hidden/promoid");
                prdData.RemoveXmlNode("genxml/hidden/promocalcdate");
                prdData.RemoveXmlNode("genxml/hidden/datefrom");
                prdData.RemoveXmlNode("genxml/hidden/dateuntil");

                // remove any sale price amounts that may have been added by group promotion.
                var l = prdData.XMLDoc.SelectNodes("genxml/models/genxml");
                if (l != null)
                {
                    var lp = 1;
                    foreach (XmlNode nod in l)
                    {
                        switch (whichprice)
                        {
                            case 1:
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid");
                                break;
                            case 2:
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealercost", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promodealercostid");
                                break;
                            case 3:
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealersale", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promodealersaleid");
                                break;
                            case 4:
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtdealersale", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promodealersaleid");
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid");
                                break;
                            default:
                                prdData.SetXmlPropertyDouble("genxml/models/genxml[" + lp + "]/textbox/txtsaleprice", "0");
                                prdData.RemoveXmlNode("genxml/models/genxml[" + lp + "]/hidden/promosalepriceid");
                                break;
                        }
                        lp += 1;
                    }
                }

                objCtrl.Update(prdData);
                foreach (var lang in cultureList)
                {
                    var prdDataLang = objCtrl.GetDataLang(productId, lang);
                    if (prdDataLang != null)
                    {
                        prdDataLang.RemoveXmlNode("genxml/hidden/promodesc");
                        objCtrl.Update(prdDataLang);
                    }
                }
            }
        }

        #endregion
    }
}
