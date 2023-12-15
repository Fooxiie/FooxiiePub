using Life.Network;
using Life;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Life.UI;
using Life.VehicleSystem;
using Life.CharacterSystem;
using Mirror.RemoteCalls;
using Mirror;
using Life.DB;
using FirstGearGames.Utilities.Networks;

namespace FooxiiePub
{
    public class FooxiiePub : Plugin
    {
        private static double PricePub { get; set; }
        public static string PubName { get; private set; }
        private static string TitleNotif { get; set; }
        private static string MessageNotif { get; set; }

        public FooxiiePub(IGameAPI api) : base(api)
        {
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            var configFilePath = Path.Combine(pluginsPath, "FooxiiePub/config.json");

            if (!Directory.Exists(Path.GetDirectoryName(configFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath) ??
                                          Path.Combine(pluginsPath, "FooxiiePub"));
            }

            if (!File.Exists(configFilePath))
            {
                File.WriteAllText(configFilePath, "{\n    \"PricePub\": 1000,\n    \"PubName\": \"PUB\",\n    \"TitleNotif\": \"Pub\",\n    \"MessageNotif\": \"Une nouvelle pub est disponible par SMS\"\n}");
            }

            var configuration = ChargerConfiguration(configFilePath);

            PricePub = configuration.PricePub;
            PubName = configuration.PubName;
            TitleNotif = configuration.TitleNotif;
            MessageNotif = configuration.MessageNotif;

            SetupCommand();
        }

        private static Config ChargerConfiguration(string cheminFichierConfig)
        {
            var jsonConfig = File.ReadAllText(cheminFichierConfig);
            return JsonConvert.DeserializeObject<Config>(jsonConfig);
        }

        private static void SetupCommand()
        {
            var sChatCommand = new SChatCommand("/pub",
                "Créer une campagne de pub par SMS",
                "/pub", (Action<Player, string[]>)((player, arg) =>
                {
                    var name = "";
                    var needPhone = false;

                    var messagePanel = new UIPanel($"Message de la pub", UIPanel.PanelType.Input)
                        .AddButton("Fermer", (ui) => { player.ClosePanel(ui); })
                        .AddButton($"Payer ({PricePub}€)", (ui) =>
                        {
                            var message = ui.inputText;

                            if (message != null && message.Length > 4 && message.Length < 300)
                            {
                                if (player.character.Money >= PricePub)
                                {
                                    player.SendText("<color=#6aa84f>Pub commandé !</color>");
                                    player.AddMoney(-PricePub, "Achat PUB SMS");
                                    SendSMS(name, message, needPhone, player.character.PhoneNumber);
                                }
                                else
                                {
                                    player.SendText("<color=#fb4039>Vous n'avez pas assez d'argent.</color>");
                                }
                            }
                            else
                            {
                                player.SendText(
                                    "<color=#fb4039>Message de pub incorrect ! Plus de 5 caractères et moins de 300.</color>");
                            }

                            player.ClosePanel(ui);
                        });

                    var needPhoneNumberPanel =
                        new UIPanel($"Ajouter le numéro de téléphone ? (Oui/Non)", UIPanel.PanelType.Text)
                            .AddButton("Oui", (ui) =>
                            {
                                needPhone = true;
                                player.ClosePanel(ui);
                                player.ShowPanelUI(messagePanel);
                            })
                            .AddButton("Non", (ui) =>
                            {
                                needPhone = false;
                                player.ClosePanel(ui);
                                player.ShowPanelUI(messagePanel);
                            });

                    var namePanel = new UIPanel($"Nom de l'envoyeur", UIPanel.PanelType.Input)
                        .AddButton("Fermer", (ui) => { player.ClosePanel(ui); })
                        .AddButton("Valider", (ui) =>
                        {
                            name = ui.inputText;
                            if (name.Length < 3)
                            {
                                player.SendText("<color=#fb4039>Vous devez entrer un nom correct.</color>");
                                player.ClosePanel(ui);
                            }
                            else
                            {
                                player.ClosePanel(ui);
                                player.ShowPanelUI(needPhoneNumberPanel);
                            }
                        });

                    player.ShowPanelUI(namePanel);
                }));

            sChatCommand.Register();
        }

        private static async void SendSMS(string name, string message, bool needPhone, string fromtel)
        {
            var list = await (from m in LifeDB.db.Table<Characters>()
                where m.AccountId >= 0
                select m).ToListAsync();
            foreach (var character in list)
            {
                await LifeDB.SendSMS(character.Id, "69", character.PhoneNumber, Nova.UnixTimeNow(),
                    needPhone ? $"[{name}]\n{message}\nTel:{fromtel}" : $"[{name}]\n{message}");
                var contacts = await LifeDB.FetchContacts(character.Id);
                var contactPub = contacts.contacts.Where(contact => contact.number == "69").ToList();
                if (contactPub.Count == 0)
                {
                    await LifeDB.CreateContact(character.Id, "69", PubName);
                }
            }

            foreach (var player in Nova.server.GetAllInGamePlayers())
            {
                player.setup.TargetUpdateSMS();
                player.Notify(TitleNotif, MessageNotif);
            }
        }
    }

    internal class Config
    {
        public double PricePub { get; set; }
        public List<String> ForbiddenWord { get; set; }
        public String PubName { get; set; }
        public String TitleNotif { get; set; }
        public String MessageNotif { get; set; }
    }
}