﻿using DS4Windows;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using DS4WinWPF.DS4Control.Profiles.Legacy;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;

namespace DS4WinWPF.DS4Control
{
    public static class OutputSlotPersist
    {
        private static async Task<IExtendedXmlSerializer> GetOutputSlotsSerializerAsync()
        {
            return await Task.Run(GetOutputSlotsSerializer);
        }

        private static IExtendedXmlSerializer GetOutputSlotsSerializer()
        {
            return new ConfigurationContainer()
                .EnableImplicitTyping(typeof(OutputSlots))
                .Type<Slot>().EnableReferences(c => c.Idx)
                .Create();
        }

        [ConfigurationSystemComponent]
        public static async Task<bool> ReadConfig(OutputSlotManager slotManager)
        {
            bool result = false;
            string output_path = Path.Combine(Global.RuntimeAppDataPath, Constants.OutputSlotsFileName);
            if (File.Exists(output_path))
            {
                /*
                 TODO: pick this up after weekend
                OutputSlots settings;

                await using (var stream = File.OpenRead(output_path))
                {
                    var serializer = await GetOutputSlotsSerializerAsync();

                    settings = await Task.Run(() => serializer.Deserialize<OutputSlots>(stream));
                }

                foreach (var slot in settings.Slots)
                {
                    
                }
                */

                XmlDocument m_Xdoc = new XmlDocument();
                try { m_Xdoc.Load(output_path); }
                catch (UnauthorizedAccessException) { }
                catch (XmlException) { }

                XmlElement rootElement = m_Xdoc.DocumentElement;
                if (rootElement == null) return false;

                foreach(XmlElement element in rootElement.GetElementsByTagName("Slot"))
                {
                    OutSlotDevice tempDev = null;
                    string temp = element.GetAttribute("idx");
                    if (int.TryParse(temp, out int idx) && idx >= 0 && idx <= 3)
                    {
                        tempDev = slotManager.OutputSlots[idx];
                    }

                    if (tempDev != null)
                    {
                        tempDev.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
                        XmlNode tempNode = element.SelectSingleNode("DeviceType");
                        if (tempNode != null && Enum.TryParse(tempNode.InnerText, out OutContType tempType))
                        {
                            tempDev.PermanentType = tempType;
                        }
                    }
                }

                result = true;
            }

            return result;
        }

        [ConfigurationSystemComponent]
        public static bool WriteConfig(OutputSlotManager slotManager)
        {
            bool result = false;
            XmlDocument m_Xdoc = new XmlDocument();
            XmlNode rootNode;
            rootNode = m_Xdoc.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
            m_Xdoc.AppendChild(rootNode);

            rootNode = m_Xdoc.CreateComment(string.Format(" Made with DS4Windows version {0} ", Global.ExecutableProductVersion));
            m_Xdoc.AppendChild(rootNode);

            rootNode = m_Xdoc.CreateWhitespace("\r\n");
            m_Xdoc.AppendChild(rootNode);

            XmlElement baseElement = m_Xdoc.CreateElement("OutputSlots", null);
            baseElement.SetAttribute("app_version", Global.ExecutableProductVersion);

            int idx = 0;
            foreach (OutSlotDevice dev in slotManager.OutputSlots)
            {
                if (dev.CurrentReserveStatus == OutSlotDevice.ReserveStatus.Permanent)
                {
                    XmlElement slotElement = m_Xdoc.CreateElement("Slot");
                    slotElement.SetAttribute("idx", idx.ToString());

                    XmlElement propElement;
                    propElement = m_Xdoc.CreateElement("DeviceType");
                    propElement.InnerText = dev.PermanentType.ToString();
                    slotElement.AppendChild(propElement);

                    baseElement.AppendChild(slotElement);
                }

                idx++;
            }

            m_Xdoc.AppendChild(baseElement);

            string output_path = Path.Combine(Global.RuntimeAppDataPath, Constants.OutputSlotsFileName);
            try { m_Xdoc.Save(output_path); result = true; }
            catch (UnauthorizedAccessException) { result = false; }

            return result;
        }
    }
}