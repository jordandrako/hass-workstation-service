using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Text.Json;
using hass_workstation_service.Communication;
using hass_workstation_service.Domain.Sensors;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace hass_workstation_service.Data
{
    public class ConfiguredSensorsService
    {
        public ICollection<AbstractSensor> ConfiguredSensors { get; private set; }
        public IConfiguration Configuration { get; private set; }
        private readonly MqttPublisher _publisher;
        private readonly IsolatedStorageFile _fileStorage;

        public ConfiguredSensorsService(MqttPublisher publisher)
        {
            this._fileStorage = IsolatedStorageFile.GetUserStoreForApplication();

            ConfiguredSensors = new List<AbstractSensor>();
            _publisher = publisher;
            ReadSettings();
        }

        public async void ReadSettings()
        {
            IsolatedStorageFileStream stream = this._fileStorage.OpenFile("configured-sensors.json", FileMode.OpenOrCreate);
            Log.Logger.Information($"reading configured sensors from: {stream.Name}");
            List<ConfiguredSensor> sensors = new List<ConfiguredSensor>();
            if (stream.Length > 0)
            {
                sensors = await JsonSerializer.DeserializeAsync<List<ConfiguredSensor>>(stream);
            }

            foreach (ConfiguredSensor configuredSensor in sensors)
            {
                AbstractSensor sensor;
                #pragma warning disable IDE0066
                switch (configuredSensor.Type)
                {
                    case "UserNotificationStateSensor":
                        sensor = new UserNotificationStateSensor(_publisher, configuredSensor.Name, configuredSensor.Id);
                        break;
                    case "DummySensor":
                        sensor = new DummySensor(_publisher, configuredSensor.Name, configuredSensor.Id);
                        break;
                    default:
                        throw new InvalidOperationException("unsupported sensor type in config");
                }
                this.ConfiguredSensors.Add(sensor);
            }
            stream.Close();
        }

        public async void WriteSettings()
        {
            IsolatedStorageFileStream stream = this._fileStorage.OpenFile("configured-sensors.json", FileMode.OpenOrCreate);
            Log.Logger.Information($"writing configured sensors to: {stream.Name}");
            List<ConfiguredSensor> configuredSensorsToSave = new List<ConfiguredSensor>();

            foreach (AbstractSensor sensor in this.ConfiguredSensors)
            {
                configuredSensorsToSave.Add(new ConfiguredSensor(){Id = sensor.Id, Name = sensor.Name, Type = sensor.GetType().Name});
            }

            await JsonSerializer.SerializeAsync(stream, configuredSensorsToSave);
            stream.Close();
        }

        public void AddConfiguredSensor(AbstractSensor sensor)
        {
            this.ConfiguredSensors.Add(sensor);
            WriteSettings();
        }
    }
}