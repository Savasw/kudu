﻿using System.Collections.Concurrent;
using Kudu.Client.Commands;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Client.SourceControl;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Hubs;
using Kudu.SignalR.Models;
using Kudu.SignalR.ViewModels;
using SignalR.Hubs;

namespace Kudu.SignalR.Infrastructure {
    public class SiteConfiguration : ISiteConfiguration {
        private static readonly ConcurrentDictionary<string, SiteConfiguration> _cache = new ConcurrentDictionary<string, SiteConfiguration>();
        private static dynamic devenvClients = Hub.GetClients<DevelopmentEnvironment>();


        public SiteConfiguration(IApplication application) {
            ServiceUrl = application.ServiceUrl;
            SiteUrl = application.SiteUrl;
            Name = application.Name;

            SiteConfiguration config;
            if (_cache.TryGetValue(Name, out config)) {
                Repository = config.Repository;
                ProjectSystem = config.ProjectSystem;
                DevProjectSystem = config.DevProjectSystem;

                
                if (config.DeploymentManager.IsActive) {
                    DeploymentManager = config.DeploymentManager;
                    CommandExecutor = config.CommandExecutor;
                    DevCommandExecutor = config.DevCommandExecutor;
                }
                else {
                    SubscribeToEvents();
                }
            }
            else {
                Repository = new RemoteRepository(ServiceUrl + "scm"); 
                ProjectSystem = new RemoteProjectSystem(ServiceUrl + "live/files");                
                DevProjectSystem = new RemoteProjectSystem(ServiceUrl + "dev/files");
                
                SubscribeToEvents();

                _cache[Name] = this;
            }
        }

        private void SubscribeToEvents() {
            DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
            DeploymentManager.StatusChanged += OnDeploymentStatusChanged;

            
            DevCommandExecutor = new RemoteCommandExecutor(ServiceUrl + "dev/command");
            DevCommandExecutor.CommandEvent += commandEvent => {
                OnNewCommandEvent(devenvClients, commandEvent);
            };

            CommandExecutor = new RemoteCommandExecutor(ServiceUrl + "live/command");
            CommandExecutor.CommandEvent += commandEvent => {
                OnNewCommandEvent(devenvClients, commandEvent);
            };
        }

        private void OnDeploymentStatusChanged(DeployResult result) {
            var clients = Hub.GetClients<Kudu.SignalR.Hubs.Deployment>();
            clients.updateDeployStatus(new DeployResultViewModel(result));
        }

        private void OnNewCommandEvent(dynamic clients, CommandEvent commandEvent) {
            if (commandEvent.EventType == CommandEventType.Complete) {
                clients.commandComplete();
            }
            else {
                clients.processCommand(commandEvent.Data);
            }
        }

        public string Name { get; private set; }
        public string ServiceUrl { get; private set; }
        public string SiteUrl { get; private set; }

        public IProjectSystem DevProjectSystem {
            get;
            private set;
        }

        public IProjectSystem ProjectSystem {
            get;
            private set;
        }

        public IRepository Repository {
            get;
            private set;
        }

        IDeploymentManager ISiteConfiguration.DeploymentManager {
            get {
                return DeploymentManager;
            }
        }

        private RemoteDeploymentManager DeploymentManager {
            get;
            set;
        }

        public ICommandExecutor CommandExecutor {
            get;
            private set;
        }

        public ICommandExecutor DevCommandExecutor {
            get;
            private set;
        }
    }
}