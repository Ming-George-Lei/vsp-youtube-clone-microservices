using Storage.Infrastructure;

namespace Storage.API.Application.Configurations {
    public class StorageConfiguration {
        public StorageType Type { get; set; } = StorageType.LocalStorage;
    }
}