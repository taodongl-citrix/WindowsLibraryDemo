using System;
using System.ComponentModel;

namespace WindowsLibraryDemo
{
    public interface IProvider : INotifyPropertyChanged, IDisposable
#if DUMP
        , IDbDump
#endif
    {
        string ID { get; }
        string FriendlyName { get; set; }
        string Description { get; set; }
        string ConfigURL { get; set; } // NB store as string incase configured with error
        string ServiceRecordId { get; set; }
        string UserName { get; set; }
        string StoreGuid { get; set; }
        string UserGuid { get; set; }
        string GetTokenExpiryValue();
        bool IsDeleting { get; }
        /*This variable is used to record when was the last time login happened for cloud store (this is used so that we can skip go online call from web ui later)*/
        double LastLoginTimeForCloud { get; set; }
        // Apply the admin setting and the user override to determine if this store is enabled
        bool Enabled { get; }
        /// <summary>
        /// This method should merge any values from new version of the resource
        /// modifying the current resource in place or by creating a new resource
        /// </summary>
        /// <param name="newVersion">A new version of the resource</param>
        /// <returns>Merged object. May be this or newVersion</returns
        bool MayChangeNameOrConfig { get; }
        bool SupportsSubscription { get; }
        bool LastEnemerationWasPartial { get; }
    }

    class Provider : IProvider
    {
        public string ID => throw new NotImplementedException();

        public string FriendlyName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ConfigURL { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ServiceRecordId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UserName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string StoreGuid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UserGuid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsDeleting => throw new NotImplementedException();

        public double LastLoginTimeForCloud { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool Enabled => throw new NotImplementedException();

        public bool MayChangeNameOrConfig => throw new NotImplementedException();

        public bool SupportsSubscription => throw new NotImplementedException();

        public bool LastEnemerationWasPartial => throw new NotImplementedException();

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string GetTokenExpiryValue()
        {
            throw new NotImplementedException();
        }
        public override string ToString()
        {
            return $"{ID}: {FriendlyName}";
        }
    }
}
