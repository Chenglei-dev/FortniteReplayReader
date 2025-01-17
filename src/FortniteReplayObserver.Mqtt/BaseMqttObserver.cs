﻿using FortniteReplayReader.Core.Contracts;
using FortniteReplayReader.Observerable.Contracts;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;

namespace FortniteReplayObservers.Mqtt
{
    public class BaseMqttObserver<T> : FortniteObserver<T>, IFortniteObserver<T>
    {
        private IDisposable unsubscriber;
        private IMqttClient mqttClient;

        protected MqttSettings Settings;

        public BaseMqttObserver()
        {
            Settings = ReadSettingsFile<MqttSettings>();

            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId($"mqttnet_{Guid.NewGuid()}")
                .WithTcpServer(Settings.HostName, Settings.Port)
                .WithCredentials(Settings.UserName, Settings.Password)
                .WithTls()
                .Build();

            mqttClient.Connected += async (s, e) =>
            {
                Console.WriteLine("### CONNECTED WITH SERVER ###");
            };

            var task = mqttClient.ConnectAsync(options);
            task.Wait();
        }

        protected virtual string CreateTopic()
        {
            return $"Fortnite/{Settings.Topic}";
        }


        protected virtual string CreateMessagePayload(T e)
        {
            return JsonConvert.SerializeObject(e);
        }

        public void OnStart()
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(CreateTopic())
                .WithPayload("{ \"started\": 1 }")
                .WithAtLeastOnceQoS()
                .WithRetainFlag(false)
                .Build();

            var task = mqttClient.PublishAsync(message);
            task.Wait();
        }

        public void OnCompleted()
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(CreateTopic())
                .WithPayload("{ \"finished\": 1 }")
                .WithAtLeastOnceQoS()
                .WithRetainFlag(false)
                .Build();

            var task = mqttClient.PublishAsync(message);
            task.Wait();

            this.Unsubscribe();
        }

        public void OnError(Exception error)
        {
            Unsubscribe();
        }

        public void OnNext(T value)
        {
            var topic = CreateTopic();
            var payload = CreateMessagePayload(value);

            if (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(payload))
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithAtLeastOnceQoS()
                    .WithRetainFlag(false)
                    .Build();

                var task = mqttClient.PublishAsync(message);
                task.Wait();
            }

        }

        public override void Subscribe(IFortniteObservable<T> provider)
        {
            if (provider != null)
            {
                unsubscriber = provider.Subscribe(this);
            }
        }

        public override void Unsubscribe()
        {
            var task = mqttClient.DisconnectAsync();
            task.Wait();

            mqttClient.Dispose();
            unsubscriber.Dispose();
        }
    }

    public class MqttSettings
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Topic { get; set; }
    }
}