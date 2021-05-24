using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monitel.Mal;
using Monitel;
using Monitel.Diogen.Core;
using Monitel.PlatformInfrastructure;
using Monitel.Mal.Providers;
using Monitel.Mal.Providers.Mal;
using Monitel.Mal.Context;
using Monitel.Mal.Context.CIM16;
using IdentifiedObject = Monitel.Mal.Context.CIM16.Names.IdentifiedObject;
using System.Threading;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace ConsoleApp13
{
    class Program
    {
        const string nullGuid = "00000000-0000-0000-0000-000000000000";
        string fileResult = @"W:\БИТ\САСДУ\08_ВНЕДРЕНИЕ ОИК СК-11 (этапы)\ТМ\прямые ссылки на формах\Формы скорректир" + "\\info.csv";
        List<Information> infoList = new List<Information>();
        ModelImage mImage;
        IEnumerable<ReplicatedAnalogValue> RAVs;
        IEnumerable<CalculatedAnalogValue> CAVs;
        IEnumerable<ReplicatedDiscreteValue> RDVs;
        IEnumerable<CalculatedDiscreteValue> CDVs;
        IEnumerable<AggregatedAnalogValue> AAVs;
        static void Main(string[] args)
        {
            Program program = new Program();
            Console.WriteLine("Введите номер модели для подключения: ");
            int modelNumb = 1672;
            try
            {
                modelNumb = Convert.ToInt32(Console.ReadLine());
            }
            catch { };
            
            MalContextParams context = new MalContextParams()
            {
                OdbServerName = "ag-lis-aipim",
                //OdbServerName = "sv-ck11-rzv",
                OdbInstanseName = "ODB_SCADA",
                OdbModelVersionId = modelNumb,
            };        
            MalProvider malProvider = new MalProvider(context, MalContextMode.Open, "test");
            program.mImage = new ModelImage(malProvider);
            Console.WriteLine("Подключение выполнено к модели "+context.OdbModelVersionId);         
            
            MetaClass RAVClass = program.mImage.MetaData.Classes["ReplicatedAnalogValue"];
            program.RAVs = program.mImage.GetObjects(RAVClass).Cast<ReplicatedAnalogValue>();

            MetaClass CAVClass = program.mImage.MetaData.Classes["CalculatedAnalogValue"];
            program.CAVs = program.mImage.GetObjects(CAVClass).Cast<CalculatedAnalogValue>();

            MetaClass RDVClass = program.mImage.MetaData.Classes["ReplicatedDiscreteValue"];
            program.RDVs = program.mImage.GetObjects(RDVClass).Cast<ReplicatedDiscreteValue>();

            MetaClass CDVClass = program.mImage.MetaData.Classes["CalculatedDiscreteValue"];
            program.CDVs = program.mImage.GetObjects(CDVClass).Cast<CalculatedDiscreteValue>();
            
            MetaClass AAVClass = program.mImage.MetaData.Classes["AggregatedAnalogValue"];
            program.AAVs = program.mImage.GetObjects(AAVClass).Cast<AggregatedAnalogValue>();
            
            Console.WriteLine("Загружены данные по реплицируемым и вычисляемым значениям измерений");
            
            string filepath = @"W:\БИТ\САСДУ\08_ВНЕДРЕНИЕ ОИК СК-11 (этапы)\ТМ\прямые ссылки на формах\Формы скорректир";
            string[] allfiles = Directory.GetFiles(filepath, "*.xml", SearchOption.AllDirectories);
            int countFile = allfiles.Count();
            int i = 1;
            foreach (string nameFile in allfiles)
            {
                List<TypeElement> typeList = new List<TypeElement>();
                program.infoList.Clear();
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(nameFile);
                XmlElement xRoot = xDoc.DocumentElement;
                XmlNodeList typeElementsForm = xRoot.SelectNodes("//ElementType");
                foreach( XmlNode typeForm in typeElementsForm)
                {
                    TypeElement typeElement=new TypeElement()
                    { TypeID= typeForm.Attributes.GetNamedItem("TypeID").Value,
                    TypeName= typeForm.Attributes.GetNamedItem("TypeName").Value};
                    typeList.Add(typeElement);
                }

                XmlNodeList childnode = xRoot.SelectNodes("//Node");
                foreach (XmlNode xnode in childnode)
                {  program.AllNode(xnode, xRoot, typeList); }
                XmlNodeList itemList = xRoot.SelectNodes("//Item");
                foreach (XmlNode item in itemList)
                {
                    if (item.Attributes.GetNamedItem("Name").Value == "Monitel.Mag.LocalExpression")
                    program.AllItem(item, xRoot);
                }
                XmlNodeList secondList = xRoot.SelectNodes("//SecondLevelElement[@OnChart='true']");
                foreach (XmlNode element in secondList)
                { program.AllGrfic(element, xRoot); }

                XmlNodeList linksList = xRoot.SelectNodes("//Link");
                foreach (XmlNode link in linksList)
                {
                    XmlNode nodeParent = link.SelectSingleNode("../..");
                    string info = "";
                    //Проверка объекта индикатора
                    Guid elementGuid = new Guid(nullGuid);
                    try
                    {
                        elementGuid = new Guid(nodeParent.Attributes.GetNamedItem("ElementUID").Value);
                    }
                    catch { };
                    if (elementGuid != new Guid(nullGuid))
                    {
                        MeasValue measValue = program.GetDirectLink(elementGuid);
                        if (measValue != null)
                        {
                            program.infoList.Add( new Information()
                            {
                                Info = "Прямая ссылка",
                                NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                                IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                                MeasValue = measValue,
                                Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                                Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                                Element = "Объект идикатора",
                            });
                        }
                    }
                    //Проверка измерения индикатора
                    elementGuid = new Guid(nullGuid);
                    try
                    {
                        elementGuid = new Guid(link.Attributes.GetNamedItem("ElementUID").Value);
                    }
                    catch { };
                    if (elementGuid != new Guid(nullGuid))                   
                    {
                        MeasValue measValue = program.GetDirectLink(elementGuid);
                        if (measValue != null)
                        {
                            info = "Прямая ссылка";
                        }
                        else
                        {
                            Guid measurementType = new Guid(link.Attributes.GetNamedItem("MeasurementType").Value);
                            Guid measurementValueType = new Guid(link.Attributes.GetNamedItem("MeasurementValueType").Value);
                            measValue = program.GetIndirectLink(elementGuid, measurementType, measurementValueType);
                            info = "Косвенная ссылка";
                        }
                        program.infoList.Add(new Information()
                        {
                            Info = info,
                            NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                            IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                            MeasValue = measValue,
                            Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                            Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                            Element = "Измерение идикатора"
                        });
                    }                        
                }
                //Пределы индикаторов
                XmlNodeList idItemList = xRoot.SelectNodes("//IdentifiedItem");
                foreach (XmlNode item in idItemList)
                {
                    XmlNode nodeParent = item.SelectSingleNode("../..");
                    Guid elementGuid = new Guid(nullGuid);
                    try
                    {
                        elementGuid = new Guid(item.Attributes.GetNamedItem("ElementUID").Value);
                    }
                    catch { };
                    if (elementGuid != new Guid(nullGuid))
                    {
                        MeasValue measValue = program.GetDirectLink(elementGuid);
                        if (measValue != null)
                        {
                            program.infoList.Add(new Information()
                            {
                                IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                                MeasValue=measValue,
                                NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                                Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                                Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                                Element = "Предел индикатора: " + item.Attributes.GetNamedItem("Name").Value,
                                Info = "Прямая ссылка"
                            });
                        }
                    }                        
                }
                program.SaveFile(program.infoList, i++, countFile);
            }
            Console.WriteLine("Запись выполнена!");           
            Console.ReadKey();
        }
        /// <summary>
        /// Запись в файл результатов
        /// </summary>
        /// <param name="listInfo"></param>
        private void SaveFile(List<Information> infoList, int i, int countFile)
        {
            var d = ";";
            if (infoList.Count() > 0)
                using (StreamWriter sw = new StreamWriter(fileResult, true, System.Text.Encoding.Default))
                {
                    sw.WriteLine("NameForm" + d + "IdNode" + d + "Uid" + d + "NameValue" + d + "ClassValue" + d
                        + "Xposition" + d + "Yposition" + d + "Info" + d + "Element" + d + "ElementPropety");
                    foreach (var infoStr in infoList)
                    {
                        try
                        {
                            sw.WriteLine(infoStr.NameForm + d + infoStr.IdNode + d + infoStr.MeasValue.Uid + d + infoStr.MeasValue.NameValue + d + infoStr.MeasValue.TypeValue + d +
                                infoStr.Xposition + d + infoStr.Yposition + d + infoStr.Info + d + infoStr.Element + d + infoStr.ElementPropety);
                        }
                        catch (NullReferenceException nullex)
                        {
                            sw.WriteLine(infoStr.NameForm + d + infoStr.IdNode + d + "-" + d + "-" + d + "-" + d +
                              infoStr.Xposition + d + infoStr.Yposition + d + infoStr.Info + d + infoStr.Element + d + infoStr.ElementPropety);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(" NodeID=" + infoStr.IdNode + " " + e.Message);
                           
                        }
                    }
                }
            Console.WriteLine("Проверено файлов " + i + " из " + countFile);
        }
        /// <summary>
        /// Получение измерения по прямой ссылке
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        private MeasValue GetDirectLink(Guid guid)
        {
            MeasValue measValue = null;

            try
            {
                bool flag = false;
                var element = mImage.GetObject(guid);                
                var elementType = element.ClassId;
                switch (elementType)
                {       
                    case 629:
                        ReplicatedAnalogValue rav = (ReplicatedAnalogValue)element;
                        measValue = GetMeasValue(rav);                        
                        break;                  
                    case 608:
                        ReplicatedDiscreteValue rdv = (ReplicatedDiscreteValue)element;
                        measValue = GetMeasValue(rdv);
                        break;
                    case 626:
                        AggregatedAnalogValue aav = (AggregatedAnalogValue)element;
                        measValue = GetMeasValue(aav);
                        break;
                    case 621:
                        CalculatedAnalogValue cav = (CalculatedAnalogValue)element;
                        measValue = GetMeasValue(cav);
                        break;
                    case 609:
                        CalculatedDiscreteValue cdv = (CalculatedDiscreteValue)element;
                        measValue = GetMeasValue(cdv);
                        break;
                    
                break;
            }
            }
            catch {

                Console.WriteLine("Проверить "+guid);
            };                  
            return measValue;
        }
        
        /// <summary>
        /// Получение измерения по косвенной ссылке
        /// </summary>
        /// <param name="elementGuid"></param>
        /// <param name="measurementType"></param>
        /// <param name="measurementValueType"></param>
        /// <returns></returns>
        private MeasValue GetIndirectLink(Guid elementGuid,Guid measurementType,Guid measurementValueType)
        {
            MeasValue measValue = null;
            bool valueFind = false;
            #region Проверка косвенной ссылки по объекту
            try
            {
                IEnumerable<ReplicatedAnalogValue> ravs = from item in RAVs
                                                          where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                          && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                          && item.Analog.PowerSystemResource.Uid.Equals(elementGuid)
                                                          select item;                
                measValue = GetMeasValue(ravs.FirstOrDefault());
                valueFind = true;
            } catch { };
            if (valueFind==false)
            {
                try
                {
                    IEnumerable<ReplicatedDiscreteValue> rdvs = from item in RDVs
                                                              where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                              && item.Discrete.MeasurementType.Uid.Equals(measurementType)
                                                              && item.Discrete.PowerSystemResource.Uid.Equals(elementGuid)
                                                              select item;                    
                    measValue = GetMeasValue(rdvs.FirstOrDefault());
                    valueFind = true;
                }
                catch { };
            }
            if (valueFind == false)
            {
                try
                {                    
                    IEnumerable<CalculatedAnalogValue> cavss = from item in CAVs
                                                                where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                                && item.Analog.PowerSystemResource.Uid.Equals(elementGuid)
                                                                select item;
                    measValue = GetMeasValue(cavss.FirstOrDefault());
                    valueFind = true;
                }
                catch { };
            }
            if (valueFind == false)
            {
                try
                {
                    IEnumerable<CalculatedDiscreteValue> cdvs = from item in CDVs
                                                                where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                && item.Discrete.MeasurementType.Uid.Equals(measurementType)
                                                                && item.Discrete.PowerSystemResource.Uid.Equals(elementGuid)
                                                                select item;
                    measValue = GetMeasValue(cdvs.FirstOrDefault());
                    valueFind = true;
                }
                catch { };
            }
            if (valueFind == false)
            {
                try
                {
                    IEnumerable<AggregatedAnalogValue> aavs = from item in AAVs
                                                              where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                              && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                              && item.Analog.PowerSystemResource.Uid.Equals(elementGuid)
                                                              select item;
                    measValue = GetMeasValue(aavs.FirstOrDefault());
                    valueFind = true;
                }
                catch { };
            }
            #endregion
            #region Проверка косвенной ссылки по привязке аналога или дискрета
            if (measValue==null)
            {
                try
                {
                    IEnumerable<ReplicatedAnalogValue> ravs = from item in RAVs
                                                              where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                              && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                              && item.Analog.Uid.Equals(elementGuid)
                                                              select item;
                    measValue = GetMeasValue(ravs.FirstOrDefault());
                    valueFind = true;
                }
                catch { };
                if (valueFind == false)
                {
                    try
                    {
                        IEnumerable<ReplicatedDiscreteValue> rdvs = from item in RDVs
                                                                    where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                    && item.Discrete.MeasurementType.Uid.Equals(measurementType)
                                                                    && item.Discrete.Uid.Equals(elementGuid)
                                                                    select item;
                        measValue = GetMeasValue(rdvs.FirstOrDefault());
                        valueFind = true;
                    }
                    catch { };
                }
                if (valueFind == false)
                {
                    try
                    {
                        IEnumerable<CalculatedAnalogValue> cavs = from item in CAVs
                                                                  where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                  && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                                  && item.Analog.Uid.Equals(elementGuid)
                                                                  select item;
                        measValue = GetMeasValue(cavs.FirstOrDefault());
                        valueFind = true;
                    }
                    catch { };
                }
                if (valueFind == false)
                {
                    try
                    {
                        IEnumerable<CalculatedDiscreteValue> cdvs = from item in CDVs
                                                                    where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                    && item.Discrete.MeasurementType.Uid.Equals(measurementType)
                                                                    && item.Discrete.Uid.Equals(elementGuid)
                                                                    select item;
                        measValue = GetMeasValue(cdvs.FirstOrDefault());
                        valueFind = true;
                    }
                    catch { };
                }
                if (valueFind == false)
                {
                    try
                    {
                        IEnumerable<AggregatedAnalogValue> aavs = from item in AAVs
                                                                  where item.MeasurementValueType.Uid.Equals(measurementValueType)
                                                                  && item.Analog.MeasurementType.Uid.Equals(measurementType)
                                                                  && item.Analog.Uid.Equals(elementGuid)
                                                                  select item;
                        measValue = GetMeasValue(aavs.FirstOrDefault());
                        valueFind = true;
                    }
                    catch { };
                }
            }
#endregion
            return measValue;
        }
       
        /// <summary>
        ///Проверка Node
        /// </summary>
        /// <param name="xnode"></param>
        /// <returns></returns>
        private void AllNode(XmlNode xnode, XmlElement xRoot, List<TypeElement> typeList )
        {
            
            string info = "";
            TypeElement typeElement = typeList.Single(x => x.TypeID == xnode.Attributes.GetNamedItem("TypeID").Value);
            Guid elementGuid = new Guid(nullGuid);
            try
            {
                elementGuid = new Guid(xnode.Attributes.GetNamedItem("ElementGuid").Value);
            }
            catch { };
            if (elementGuid != new Guid(nullGuid))
            {
                MeasValue measValue =  GetDirectLink(elementGuid);
                if (measValue != null)
                {
                    info = "Прямая ссылка";
                }
                else
                {
                    Guid measurementType = new Guid(xnode.Attributes.GetNamedItem("MeasurementType").Value);
                    Guid measurementValueType = new Guid(xnode.Attributes.GetNamedItem("MeasurementValueType").Value);
                    measValue = GetIndirectLink(elementGuid, measurementType, measurementValueType);
                    info = "Косвенная ссылка";
                }
                //if (measValue != null)
                //{
                    string element=null;
                    string elementPropety=null;
                    if (typeElement.TypeName== "Monitel.Diogen.Elements.DevExpIndicators.LinearIndicator")
                    {
                        element = "Линейный индикатор";
                    }
                    else 
                    {
                        element = "Строка: " + xnode.Attributes.GetNamedItem("Row").Value;
                        elementPropety = " Столбец:" + xnode.Attributes.GetNamedItem("Column").Value;
                    }
                    var inf = new Information()
                    {
                        Info = info,
                        NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                        IdNode = xnode.Attributes.GetNamedItem("NodeID").Value,
                        MeasValue = measValue,
                        Xposition = xnode.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                        Yposition = xnode.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                        Element = element,
                        ElementPropety = elementPropety
                    };
                    infoList.Add(inf);
                //}      

            }
        }
        /// <summary>
        /// Проверка формул
        /// </summary>
        /// <param name="item"></param>
        /// <param name="xRoot"></param>
        /// <returns></returns>
        private void AllItem(XmlNode item, XmlElement xRoot)
        {
            string info = "";
            XmlNode nodeParent = item.SelectSingleNode("../..");
            XmlNodeList exprList = item.SelectNodes("*/*");
            foreach (XmlElement expr in exprList)
            {
                foreach (XmlNode operand in expr.SelectNodes("*/*/Operand"))
                {                    
                    try
                    {
                        MeasValue measValue = null;
                        if (operand.Attributes.GetNamedItem("Type").Value == "MeasValueOperand")
                        {
                            measValue = GetDirectLink(new Guid(operand.SelectSingleNode("MeasurementValue").InnerText));
                            if (measValue != null)
                                info = "Прямая ссылка";
                            else info = "Прямая ссылка не найдена";
                            infoList.Add(new Information()
                            {
                                NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                                IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                                MeasValue = measValue,
                                Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                                Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                                Element = "Формула: " + expr.Attributes.GetNamedItem("name").Value,
                                ElementPropety = " Операнд:" + operand.Attributes.GetNamedItem("Name").Value,
                                Info = info
                            });
                        }
                        else if (operand.Attributes.GetNamedItem("Type").Value == "PSRMeasOperand")
                        {
                            Guid powerSystemResource = new Guid(operand.SelectSingleNode("PowerSystemResource").InnerText);
                            Guid measurementType = new Guid(operand.SelectSingleNode("MeasurementType").InnerText);
                            Guid valueType = new Guid(operand.SelectSingleNode("ValueType").InnerText);                           
                                
                            measValue = GetIndirectLink(powerSystemResource, measurementType, valueType);
                            if (measValue != null)
                                info = "Косвенная ссылка";
                            else info = "Косвенная ссылка не найдена";
                            infoList.Add(new Information()
                            {
                                NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                                IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                                MeasValue = measValue,
                                Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                                Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                                Element = "Формула: " + expr.Attributes.GetNamedItem("name").Value,
                                ElementPropety = " Операнд:" + operand.Attributes.GetNamedItem("Name").Value,
                                Info = info
                            });
                        }                        
                        
                    }
                    catch (System.NullReferenceException)
                    {
                        Console.WriteLine( "Проверить формулу: " + expr.Attributes.GetNamedItem("name").Value);
                    }
                    
                    
                }
            }


        }
        /// <summary>
        /// Проверка графиков
        /// </summary>
        /// <param name="element"></param>
        /// <param name="xRoot"></param>
        /// <returns></returns>
        private void AllGrfic(XmlNode element, XmlElement xRoot)
        {            
            XmlNode nodeParent = element.SelectSingleNode("../..");
            string info = "";
            Guid elementGuid = new Guid(nullGuid);
            try
            {
                elementGuid = new Guid(element.Attributes.GetNamedItem("ElementGuid").Value);
            }
            catch { };
            if (elementGuid != new Guid(nullGuid))
            {
                MeasValue measValue = GetDirectLink(elementGuid);
                if (measValue != null)
                {
                    info = "Прямая ссылка";
                }
                else
                {
                    Guid measurementType = new Guid(element.Attributes.GetNamedItem("MeasurementType").Value);
                    Guid measurementValueType = new Guid(element.Attributes.GetNamedItem("MeasurementValueType").Value);
                    measValue = GetIndirectLink(elementGuid, measurementType, measurementValueType);
                    info = "Косвенная ссылка";
                }
                var inf = new Information()
                {
                    Info = info,
                    NameForm = xRoot.Attributes.GetNamedItem("Description").Value,
                    IdNode = nodeParent.Attributes.GetNamedItem("NodeID").Value,
                    MeasValue = measValue,
                    Xposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("X").Value,
                    Yposition = nodeParent.SelectSingleNode("GeometricOrigin").Attributes.GetNamedItem("Y").Value,
                    Element = "График: " + element.Attributes.GetNamedItem("UserName").Value
                };
                infoList.Add(inf);
            }                
        }
        class MeasValue
        {
            /// <summary>
            /// UID значения измерения
            /// </summary>
            public Guid Uid { get; set; }
            /// <summary>
            /// Имя значения измерения
            /// </summary>
            public string NameValue { get; set; }
            /// <summary>
            /// Тип значения измерения
            /// </summary>
            public string TypeValue { get; set; }
            /// <summary>
            /// Источник значения
            /// </summary>
            public string IdSourceValue { get; set; }
        }

        #region Конвертация измерений в MeasValue
        /// <summary>
        /// Конвертация реплицируемого аналогового значения
        /// </summary>
        /// <param name="rav"></param>
        /// <returns></returns>
        private MeasValue GetMeasValue(ReplicatedAnalogValue rav)
        {
            MeasValue measValue = new MeasValue()
            {
                Uid = rav.Uid,
                NameValue = rav.name,
                TypeValue = rav.MeasurementValueType.name
            };
            return measValue;
        }
        /// <summary>
        /// Конвертация реплицируемого дискретного значения
        /// </summary>
        /// <param name="rdv"></param>
        /// <returns></returns>
        private MeasValue GetMeasValue(ReplicatedDiscreteValue rdv)
        {
            MeasValue measValue = new MeasValue()
            {
                Uid = rdv.Uid,
                NameValue = rdv.name,
                TypeValue = rdv.MeasurementValueType.name
            };
            return measValue;
        }
        /// <summary>
        /// Конвертация агрегируемого аналогового значения
        /// </summary>
        /// <param name="aav"></param>
        /// <returns></returns>
        private MeasValue GetMeasValue(AggregatedAnalogValue aav)
        {
            MeasValue measValue = new MeasValue()
            {
                Uid = aav.Uid,
                NameValue = aav.name,
                TypeValue = aav.MeasurementValueType.name
            };
            return measValue;
        }
        /// <summary>
        /// Конвертация вычисляемого аналогового значения
        /// </summary>
        /// <param name="cav"></param>
        /// <returns></returns>
        private MeasValue GetMeasValue(CalculatedAnalogValue cav)
        {
            MeasValue measValue = new MeasValue()
            {
                Uid = cav.Uid,
                NameValue = cav.name,
                TypeValue = cav.MeasurementValueType.name
            };
            return measValue;
        }
        /// <summary>
        /// Конвертация вычисляемого дискретного значения
        /// </summary>
        /// <param name="cdv"></param>
        /// <returns></returns>
        private MeasValue GetMeasValue(CalculatedDiscreteValue cdv)
        {
            MeasValue measValue = new MeasValue()
            {
                Uid = cdv.Uid,
                NameValue = cdv.name,
                TypeValue = cdv.MeasurementValueType.name
            };
            return measValue;
        }
#endregion
        class Information
        {
            public string NameForm { get; set; }
            public string IdNode { get; set; }
            
            public MeasValue MeasValue { get; set; }

            public string Xposition { get; set; }
            public string Yposition { get; set; }
           
            public string Info { get; set; }
            public string Element { get; set; }
            public string ElementPropety { get; set; }

        }
        class TypeElement
        {
            public string TypeName { get; set; }
            public string TypeID { get; set; }
        }
    }
}
