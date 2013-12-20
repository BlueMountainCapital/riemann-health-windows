using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace RiemannHealth
{
    public class ServiceElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)base["name"]; }
            set { base["name"] = value; }
        }


        internal string Key
        {
            get { return Name; }
        }
    }

    [ConfigurationCollection(typeof(ServiceElement), AddItemName = "service", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ServiceElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ServiceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ServiceElement)element).Key;
        }

        public void Add(ServiceElement element)
        {
            BaseAdd(element);
        }

        public void Clear()
        {
            BaseClear();
        }

        public int IndexOf(ServiceElement element)
        {
            return BaseIndexOf(element);
        }

        public void Remove(ServiceElement element)
        {
            if (BaseIndexOf(element) >= 0)
            {
                BaseRemove(element.Key);
            }
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public ServiceElement this[int index]
        {
            get { return (ServiceElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }
    }

    public class ServiceInfoSection : ConfigurationSection
    {
        private static readonly ConfigurationProperty _propServiceInfo = new ConfigurationProperty(
                null,
                typeof(ServiceElementCollection),
                null,
                ConfigurationPropertyOptions.IsDefaultCollection
        );

        private static ConfigurationPropertyCollection _properties = new ConfigurationPropertyCollection();

        static ServiceInfoSection()
        {
            _properties.Add(_propServiceInfo);
        }

        [ConfigurationProperty("", Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public ServiceElementCollection Services
        {
            get { return (ServiceElementCollection)base[_propServiceInfo]; }
        }
    }
}