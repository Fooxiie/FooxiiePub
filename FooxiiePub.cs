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
        public static double PricePub { get; private set; }

        public static List<string> ForbiddenWord { get; private set; }
        public static string PubName { get; private set; }
        public static string TitleNotif { get; private set; }
        public static string MessageNotif { get; private set; }

        public FooxiiePub(IGameAPI api) : base(api) { }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            var configFilePath = Path.Combine(pluginsPath, "FooxiiePub/config.json");

            Config configuration = ChargerConfiguration(configFilePath);

            PricePub = configuration.PricePub;
            ForbiddenWord = configuration.ForbiddenWord;
            PubName = configuration.PubName;
            TitleNotif = configuration.TitleNotif;
            MessageNotif = configuration.MessageNotif;

            SetupCommand();
        }

        static Config ChargerConfiguration(string cheminFichierConfig)
        {
            string jsonConfig = File.ReadAllText(cheminFichierConfig);
            return JsonConvert.DeserializeObject<Config>(jsonConfig);
        }

        private static void SetupCommand()
        {
            SChatCommand sChatCommand = new SChatCommand("/pub",
                            "Créer une campagne de pub par SMS",
                            "/pub", (Action<Player, string[]>)((player, arg) =>
                            {
                                string name = "";
                                bool needPhone = false;

                                UIPanel messagePanel = new UIPanel($"Message de la pub", UIPanel.PanelType.Input)
                                .AddButton("Fermer", (ui) =>
                                {
                                    player.ClosePanel(ui);
                                })
                                .AddButton($"Payer ({PricePub}€)", (ui) =>
                                {
                                    string message = ui.inputText;

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
                                        player.SendText("<color=#fb4039>Message de pub incorrect ! Plus de 5 caractères et moins de 300.</color>");
                                    }
                                    player.ClosePanel(ui);
                                });

                                UIPanel needPhoneNumberPanel = new UIPanel($"Ajouter le numéro de téléphone ? (Oui/Non)", UIPanel.PanelType.Text)
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

                                UIPanel namePanel = new UIPanel($"Nom de l'envoyeur", UIPanel.PanelType.Input)
                                .AddButton("Fermer", (ui) =>
                                {
                                    player.ClosePanel(ui);
                                })
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
            List<Characters> list = await (from m in LifeDB.db.Table<Characters>()
                                           where m.AccountId >= 0
                                           select m).ToListAsync();
            foreach (Characters character in list)
            {
                if (needPhone)
                {
                    LifeDB.SendSMS(character.Id, PubName, character.PhoneNumber, Nova.UnixTimeNow(), $"[{name}]\n{message}\nTel:{fromtel}");
                }
                else
                {
                    LifeDB.SendSMS(character.Id, PubName, character.PhoneNumber, Nova.UnixTimeNow(), $"[{name}]\n{message}");
                }
            }

            foreach (var player in Nova.server.GetAllInGamePlayers())
            {
                player.setup.TargetUpdateSMS();
                player.Notify(TitleNotif, MessageNotif);
            }
        }
    }

    class Config
    {
        public double PricePub { get; set; }
        public List<String> ForbiddenWord { get; set; }
        public String PubName { get; set; }
        public String TitleNotif { get; set; }
        public String MessageNotif { get; set; }
    }
}
