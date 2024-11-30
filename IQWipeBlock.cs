﻿using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using System.Text;
using Network;
using ConVar;

namespace Oxide.Plugins
{
    [Info("IQWipeBlock", "Skuli Dropek", "1.1.23")]
    [Description("Блокируй по умному с IQWipeBlock")]
    class IQWipeBlock : RustPlugin
    {
        /// <summary>
        /// Обновление 1.0.х
        /// - Добавлена возможность выбрать формат времени "Использовать формат времени (Полный - Д + Ч + М(/С) - true / Сокращенный - Д/Ч/М/С - false)"
        /// - Добавлена возможность установить изображение на задний фон - "Ссылка на свой задний фон(Если не требуется, оставьте поле пустым)"
        /// Обновление 1.1.23
        /// - Теперь если заблокирован rifle.ak - автоматически блокируется ледянной АК (rifle.ak.ice)
        /// </summary>

        private const Boolean LanguageEn = false;


        #region Reference
        [PluginReference] Plugin IQChat, ImageLibrary, Battles, Duel, Duelist; // https://umod.org/plugins/duelist

        #region IQChat
        public void SendChat(string Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            var Chat = config.GeneralSetting.ReferenceSettings.ChatSettings;
            if (IQChat)
                if (Chat.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        #endregion

        #region ImageLibrary
        private String GetImage(String fileName, UInt64 skin = 0)
        {
            var imageId = (String)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return String.Empty;
        }
        public Boolean AddImage(String url, String shortname, UInt64 skin = 0) => (Boolean)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public void SendImage(BasePlayer player, String imageName, UInt64 imageId = 0) => ImageLibrary?.Call("SendImage", player, imageName, imageId);
        public Boolean HasImage(String imageName) => (Boolean)ImageLibrary?.Call("HasImage", imageName);
        #endregion

        #region IsDuel
        public Boolean IsDuel(UInt64 userID)
        {
            if (Battles)
                return (Boolean)Battles?.Call("IsPlayerOnBattle", userID);
            else if (Duel) return (Boolean)Duel?.Call("IsPlayerOnActiveDuel", BasePlayer.FindByID(userID));
            else if (Duelist) return (Boolean)Duelist?.Call("inEvent", BasePlayer.FindByID(userID));
            else return false;
        }
        #endregion

        #endregion

        #region Vars
        private Boolean IsUnlockedAll = false;
        readonly static String PermissionIgnoreBlock = "iqwipeblock.ignore";
        public enum TypeLock
        {
            Locked,
            Fire,
            Flash
        }
        #endregion

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Setting up Locks" : "Настройка блокировок")]
            public Blocks Block = new Blocks();
            [JsonProperty(LanguageEn ? "Configuring the interface" : "Настройка интерфейса")]
            public Interfaces Interface = new Interfaces();
            [JsonProperty(LanguageEn ? "Configuring the plugin" : "Настройка плагина")]
            public GeneralSettings GeneralSetting = new GeneralSettings();

            internal class GeneralSettings
            {
                [JsonProperty(LanguageEn ? "Use the label in the player's inventory if the item is locked(the label will be right on the item being locked)" : "Использовать метку в инвентаре игрока если предмет заблокирован(метка будет прям на предмете блокировки)")]
                public Boolean UseFlags;
                [JsonProperty(LanguageEn ? "Enable menu to open wipeblock" : "Включить меню для открытия вайпблока")]
                public Boolean UsePanelButton;
                [JsonProperty(LanguageEn ? "Enable the ability to hide the menu to users" : "Включить возможность скрыть меню пользователям")]
                public Boolean UseHidePanelButton;
                [JsonProperty(LanguageEn ? "Select the label type : 0 - Grid, 1-Flame, 2-Lightning" : "Выберите тип метки : 0 - Сетка, 1 - Пламя, 2 - Молния")]
                public TypeLock TypeLock;
                [JsonProperty(LanguageEn ? "Display time on all blocked items, regardless of progress" : "Отображать время на всех заблокированных предметах, независимо от прогресса")]
                public Boolean UseShowAllTime;

                [JsonProperty(LanguageEn ? "Settings for collaboration with other plugins" : "Настройки совместной работы с другими плагинами")]
                public ReferenceSetting ReferenceSettings = new ReferenceSetting();
                [JsonProperty(LanguageEn ? "Notify players that all items have been fully unlocked (true - yes/false - no). The message is configured in lang" : "Уведомлять игроков о том, что произошла полная разблокировка всех предметов (true - да/false - нет). Сообщение настраивается в lang")]
                public Boolean AlertAllUsersUnlocked = true;
                [JsonProperty(LanguageEn ? "Notify players every time they log on to the server that all items are unlocked (true - yes/false - no). The message is configured in lang" : "Уведомлять игроков при каждом входе на сервер, что все предметы разблокированы (true - да/false - нет). Сообщение настраивается в lang")]
                public Boolean AlertConnectedUserUnlocked = false;
                internal class ReferenceSetting
                {
                    [JsonProperty(LanguageEn ? "IQChat : Chat Settings" : "IQChat : Настройки чата")]
                    public ChatSetting ChatSettings = new ChatSetting();
                    internal class ChatSetting
                    {
                        [JsonProperty(LanguageEn ? "IQChat : Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
                        public String CustomPrefix = "[IQWipeBlock]";
                        [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                        public String CustomAvatar = "";
                        [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI уведомления")]
                        public Boolean UIAlertUse = false;
                    }
                }
            }

            internal class Blocks
            {
                [JsonProperty(LanguageEn ? "Configuring weapon and Tool Locks" : "Настройка блокировок оружия и инструментов")]
                public Dictionary<String, BlockElement> BlockWeaponAndTools = new Dictionary<String, BlockElement>();
                [JsonProperty(LanguageEn ? "Configuring Gear locks" : "Настройка блокировок снаряжения")]
                public Dictionary<String, BlockElement> BlockArmory = new Dictionary<String, BlockElement>();
                [JsonProperty(LanguageEn ? "Setting up explosive locks" : "Настройка блокировок взрывчатки")]
                public Dictionary<String, BlockElement> BlockBoom = new Dictionary<String, BlockElement>();
                [JsonProperty(LanguageEn ? "Unlock additional items after unlocking the main one (true) or jointly (false)" : "Разблокировку дополнительных предметов запускать после разблокировки основного (true) или совместно (false)")]
                public Boolean VaribleUnlockMore;

                internal class BlockElement
                {
                    [JsonProperty(LanguageEn ? "Time to lock this item(in seconds)" : "Время блокировки данного предмета(в секундах)")]
                    public Int32 TimeBlock;
                    [JsonProperty(LanguageEn ? "SkinID for the item(if not required, leave the value 0)" : "SkinID для предмета(если не требуется, оставьте значение 0)")]
                    public UInt64 SkinID;
                    [JsonProperty(LanguageEn ? "Additional list related to this subject! (Items that can be applied to the main item, example Weapons - > Ammo)" : "Дополнительный список, относящийся к этому предмету! (Предметы, которые можно применить к основному предмету, пример Оружие -> Патроны)")]
                    public Dictionary<String, Int32> BlockMoreList = new Dictionary<string, int>();
                }
            }

            internal class Interfaces
            {
                [JsonProperty(LanguageEn ? "In which part of the screen will the interface with the lock of weapons and tools be located(0-Left, 1-Center, 2-Right)" : "В какой части экрана будет расположен интерфейс с блокировкой оружия и инструментов(0 - Слева, 1 - Центр, 2 - Справа)")]
                public AlignCategory CategoryWeaponTools;
                [JsonProperty(LanguageEn ? "In which part of the screen will the interface with the equipment lock be located (0-Left, 1-Center, 2-Right)" : "В какой части экрана будет расположен интерфейс с блокировкой снаряжения (0 - Слева, 1 - Центр, 2 - Справа)")]
                public AlignCategory CategoryArmory;
                [JsonProperty(LanguageEn ? "In which part of the screen will the interface with blocking explosives and ammunition be located(0-On the Left, 1-In the Center, 2-on the Right)" : "В какой части экрана будет расположен интерфейс с блокировкой взрывчатки и боеприпасов(0 - Слева, 1 - Центр, 2 - Справа)")]
                public AlignCategory CategoryBoom;
                [JsonProperty(LanguageEn ? "Display the progress of opening an item by filling in the background" : "Отображать прогресс открытия предмета заполнением заднего фона")]
                public Boolean UseProgressiveBackground;
                [JsonProperty(LanguageEn ? "Display information-instructions, which block is responsible for what" : "Отображать информацию-инструкцию, какой блок за что отвечает")]
                public Boolean ShowInformationBlocks;

                [JsonProperty(LanguageEn ? "Link to your background (If not required, leave the field blank)" : "Ссылка на свой задний фон(Если не требуется, оставьте поле пустым)")]
                public String BackgroundUrl = String.Empty;

                [JsonProperty(LanguageEn ? "HEX background color" : "HEX цвет заднего фона")]
                public String BacgkroundColor;
                [JsonProperty(LanguageEn ? "HEX background color blurr" : "HEX цвет блюра заднего фона")]
                public String BackgroundBlurColor;
                [JsonProperty(LanguageEn ? "HEX background color blurr of additional items" : "HEX цвет блюра заднего фона дополнительных предметов")]
                public String BackgroundMoreBlurColor;

                [JsonProperty(LanguageEn ? "HEX text color" : "HEX цвет текста")]
                public String Labels;

                [JsonProperty(LanguageEn ? "HEX line color" : "HEX цвет линий")]
                public String Lines;
                [JsonProperty(LanguageEn ? "HEX line color when unblocking" : "HEX цвет линий при разблокировке")]
                public String LinesUnblock;

                [JsonProperty(LanguageEn ? "HEX background color of the blocked image" : "HEX цвет заднего фона заблокированного")]
                public String BlockedPanel;
                [JsonProperty(LanguageEn ? "HEX color of the background color of the blocked background" : "HEX цвет блюра заднего фона заблокированного")]
                public String BlurBlockedPanel;
                [JsonProperty(LanguageEn ? "HEX background color of the next item to unlock" : "HEX цвет заднего фона следующего предмета под разблокировку")]
                public String NextBlockedPanel;
                [JsonProperty(LanguageEn ? "HEX background color of the unlocked item" : "HEX цвет заднего фона разблокированного предмета")]
                public String UnblockedPanel;
                [JsonProperty(LanguageEn ? "HEX background color of an unlocked item with additional locks" : "HEX цвет заднего фона разблокированного предмета с дополнительными блокировками")]
                public String UnblockedMorePanel;
                [JsonProperty(LanguageEn ? "HEX color of the subject lines with additional locks" : "HEX цвет линий предмета с дополнительными блокировками")]
                public String PreBlockedMoreLine;
                [JsonProperty(LanguageEn ? "Sprite of the blocked element" : "Sprite заблокированного элемента")]
                public String SpriteBlocked;
                [JsonProperty(LanguageEn ? "Sprite in the quick access menu" : "Sprite в меню быстрого доступа")]
                public String SpriteBlockLogo;
                [JsonProperty(LanguageEn ? "Should I hide the interface opening button after unlocking all items" : "Скрывать ли кнопку открытия интерфейса после разблока всех предметов")]
                public Boolean ButtonIsBlock;
                [JsonProperty(LanguageEn ? "Use the time format (Full - D + H + M(/S) - true / Abbreviated - D/H/M/S - false)" : "Использовать формат времени (Полный - Д + Ч + М(/С) - true / Сокращенный - Д/Ч/М/С - false)")]
                public Boolean UseFormatTime = false;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    GeneralSetting = new GeneralSettings
                    {
                        UsePanelButton = true,
                        UseHidePanelButton = true,
                        UseFlags = true,
                        TypeLock = TypeLock.Locked,
                        UseShowAllTime = false,
                    },
                    Block = new Blocks
                    {
                        VaribleUnlockMore = true,
                        BlockWeaponAndTools = new Dictionary<String, Blocks.BlockElement>
                        {
                            ["rifle.ak"] = new Blocks.BlockElement
                            {
                                TimeBlock = 500,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>
                                {
                                    ["weapon.mod.8x.scope"] = 100,
                                }
                            },
                            ["wood"] = new Blocks.BlockElement
                            {
                                TimeBlock = 500,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>()
                            },
                            ["stones"] = new Blocks.BlockElement
                            {
                                TimeBlock = 600,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>()
                            },
                            ["sulfur.ore"] = new Blocks.BlockElement
                            {
                                TimeBlock = 700,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>()
                            },
                        },
                        BlockArmory = new Dictionary<String, Blocks.BlockElement>
                        {
                            ["shoes.boots"] = new Blocks.BlockElement
                            {
                                TimeBlock = 600,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>()
                            },
                        },
                        BlockBoom = new Dictionary<String, Blocks.BlockElement>
                        {
                            ["explosive.timed"] = new Blocks.BlockElement
                            {
                                TimeBlock = 7770,
                                SkinID = 0,
                                BlockMoreList = new Dictionary<String, Int32>()
                            },
                        }
                    },
                    Interface = new Interfaces
                    {
                        BackgroundUrl = String.Empty,
                        UseProgressiveBackground = false,
                        ShowInformationBlocks = true,
                        CategoryWeaponTools = AlignCategory.Left,
                        CategoryArmory = AlignCategory.Middle,
                        CategoryBoom = AlignCategory.Right,
                        BacgkroundColor = "#3B3A2EC3",
                        BackgroundBlurColor = "#00000044",
                        BackgroundMoreBlurColor = "#00000076",
                        Labels = "#efedee",
                        Lines = "#5E5E5EC8",
                        UnblockedPanel = "#667345",
                        BlockedPanel = "#16161647",
                        BlurBlockedPanel = "#16161624",
                        NextBlockedPanel = "#161616FF",
                        LinesUnblock = "#7D904EFF",
                        UnblockedMorePanel = "#C67036",
                        PreBlockedMoreLine = "#D08654FF",
                        SpriteBlocked = "assets/icons/bp-lock.png",
                        SpriteBlockLogo = "assets/icons/warning_2.png",
                        ButtonIsBlock = false,
                        UseFormatTime = false,
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning(LanguageEn ? $"Error #58 reading configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка #58 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        [JsonProperty(LanguageEn ? "" :"Сдвиг времени в секундах")]
        public Int32 SkipTimeBlocked = 0;
        [JsonProperty(LanguageEn ? "" :"Дата с информацией о игроках")]
        public Dictionary<UInt64, Boolean> BlockHideInfo = new Dictionary<UInt64, Boolean>();
        void ReadData() 
        {
            SkipTimeBlocked = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Int32>("IQWipeBlock/SkipTimeBlocked");
            BlockHideInfo = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, Boolean>>("IQWipeBlock/HideStatus");
        }
        void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQWipeBlock/HideStatus", BlockHideInfo);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQWipeBlock/SkipTimeBlocked", SkipTimeBlocked);
        }
        void RegisteredUser(BasePlayer player)
        {
            if (BlockHideInfo.ContainsKey(player.userID)) return;
            BlockHideInfo.Add(player.userID, false);
        }
        #endregion

        #region UI
        public enum AlignCategory
        {
            Left,
            Middle,
            Right
        }
        public readonly String IQWIPE_PARENT = "UI_IQWIPE_PARENT_MAIN";
        public readonly String IQWIPE_PARENT_PANEL_BTN = "IQWIPE_PARENT_PANEL_BTN";

        #region Interface Wipe Block

        void UI_IQ_WipeBlock(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, IQWIPE_PARENT);
            CuiElementContainer container = new CuiElementContainer();
            Single FadeIn = 0.2f;
            var Interface = config.Interface;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BacgkroundColor) }
            }, "Overlay", IQWIPE_PARENT);

            if (!String.IsNullOrWhiteSpace(config.Interface.BackgroundUrl))
            {
                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT,
                    Components =
                    {
                        new CuiRawImageComponent {  Png = GetImage($"BACKGROUND_{config.Interface.BackgroundUrl}") },
                        new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1" },
                    }
                });
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BackgroundBlurColor), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "BLUR");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.155721939 0.9231482", AnchorMax = "0.8375002 0.9842593" }, 
                Text = { FadeIn = FadeIn, Text = GetLang("TITLE_BLOCK", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

            #region Information Block

            if (Interface.ShowInformationBlocks)
            {
                #region Blocked

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT,
                    Name = IQWIPE_PARENT + "INFO_BLOCKED",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BlockedPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = "0.01354169 0.9546172", AnchorMax = "0.03020836 0.9842459", OffsetMax = "0 1", OffsetMin = "0 0.5" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.Lines), Distance = "-1.35 1.4", UseGraphicAlpha = true }
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 -1", OffsetMax = "0 -1" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BlurBlockedPanel), Material = "assets/content/ui/uibackgroundblur.mat" }
                }, IQWIPE_PARENT + "INFO_BLOCKED", IQWIPE_PARENT + "INFO_BLOCKED" + "BLUR");

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT + "INFO_BLOCKED" + "BLUR",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Interface.SpriteBlocked, Color = HexToRustFormat(Interface.Labels) },
                        new CuiRectTransformComponent{ AnchorMin = "0.33 0.3399995", AnchorMax = $"0.65 0.6599998"},
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.03281251 0.9546172", AnchorMax = "0.1208333 0.9842459" },
                    Text = { FadeIn = FadeIn, Text = GetLang("TITLE_INFO_BLOCKED", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

                #endregion

                #region Unblocked

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT,
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.UnblockedPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = $"0.01354169 0.9175813", AnchorMax = $"0.03020836 0.94721", OffsetMax = "0 1", OffsetMin = "0 1" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.LinesUnblock), Distance = "-1.35 1.35", UseGraphicAlpha = true }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.03281251 0.9175813", AnchorMax = "0.1208333 0.94721" },
                    Text = { FadeIn = FadeIn, Text = GetLang("TITLE_INFO_UNBLOCKED", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

                #endregion

                #region PreBlocked

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT,
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.NextBlockedPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = $"0.1239583 0.9546172", AnchorMax = $"0.1406247 0.9842459", OffsetMax = "0 1", OffsetMin = "0 1" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.Lines), Distance = "-1.35 1.35", UseGraphicAlpha = true }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1432293 0.9546172", AnchorMax = "0.2786458 0.9842459" },
                    Text = { FadeIn = FadeIn, Text = GetLang("TITLE_INFO_PREBLOCKED", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

                #endregion

                #region MoreBlocked

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT,
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.UnblockedMorePanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = $"0.1239583 0.9175813", AnchorMax = $"0.1406247 0.94721", OffsetMax = "0 1", OffsetMin = "0 1" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.PreBlockedMoreLine), Distance = "-1.35 1.35", UseGraphicAlpha = true }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1432293 0.9175813", AnchorMax = "0.2786458 0.94721" },
                    Text = { FadeIn = FadeIn, Text = GetLang("TITLE_INFO_MOREBLOCKED", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

                #endregion
            }

            #endregion

            container.Add(new CuiButton
            {
                FadeOut = 0.2f,
                RectTransform = { AnchorMin = $"0.8151017 0.9231482", AnchorMax = $"0.995831 0.9842593" },
                Button = { Close = IQWIPE_PARENT, Color = "0 0 0 0" },
                Text = { Text = GetLang("TITLE_BUTTON_CLOSE", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Align = TextAnchor.MiddleCenter }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "BUTTON_CLOSE");

            UI_Category_Show(player, container, Interface, config.Block.BlockWeaponAndTools, config.Interface.CategoryWeaponTools);
            UI_Category_Show(player, container, Interface, config.Block.BlockArmory, config.Interface.CategoryArmory);
            UI_Category_Show(player, container, Interface, config.Block.BlockBoom, config.Interface.CategoryBoom);
            CuiHelper.AddUi(player, container);
            if (config.GeneralSetting.UsePanelButton)
                HideButtonIsMenu(player);
        }

        void HideButtonIsMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, IQWIPE_PARENT + "HIDE_BTN");
            CuiElementContainer container = new CuiElementContainer();
            Single FadeIn = 0.2f;
            var Interface = config.Interface;
            String LangKey = BlockHideInfo[player.userID] ? GetLang("UI_PANEL_BUTTON_HIDE_ISMENU_ON", player.UserIDString) : GetLang("UI_PANEL_BUTTON_HIDE_ISMENU_OFF", player.UserIDString);

            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.1557295 0.90371939", AnchorMax = "0.8375002 0.9351867" }, 
                Button = { Command = "iqwb hide.panel.btn true", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = LangKey, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "HIDE_BTN");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Interface Category

        void UI_Category_Show(BasePlayer player, CuiElementContainer container, Configuration.Interfaces Interface, Dictionary<String, Configuration.Blocks.BlockElement> CategoryList, AlignCategory Align)
        {
            if (CategoryList == null || CategoryList.Count == 0) return;
            var Block = config.Block;
            Single FadeIn = 0.2f;
            String TitleX = Align == AlignCategory.Left ? "0.08593746 0.8481482" : Align == AlignCategory.Middle ? "0.4072909 0.8481482" : "0.7161464 0.8481482";
            String TitleY = Align == AlignCategory.Left ? "0.273958 0.8851852" : Align == AlignCategory.Middle ? "0.5859324 0.8851852" : "0.9192606 0.8851852";
            String TitleText = Align == Interface.CategoryWeaponTools ? "TITLE_CATEGORY_WEAPON_TOOL" : Align == Interface.CategoryArmory ? "TITLE_CATEGORY_ATTIRE" : "TITLE_CATEGORY_BOOM";
            String PanelX = Align == AlignCategory.Left ? "0.05208334 0" : Align == AlignCategory.Middle ? "0.3687498 0" : "0.6864532 0";
            String PanelY = Align == AlignCategory.Left ? "0.3130208 0.8351786" : Align == AlignCategory.Middle ? "0.6296868 0.8351786" : "0.9473904 0.8351786";
            String LineX = Align == AlignCategory.Left ? "0.08593746 0.8444445" : Align == AlignCategory.Middle ? "0.4026018 0.8444445" : "0.7244719 0.8444445";
            String LineY = Align == AlignCategory.Left ? "0.273958 0.845" : Align == AlignCategory.Middle ? "0.5906243 0.845" : "0.9124945 0.845";

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = TitleX, AnchorMax = TitleY },
                Text = { FadeIn = FadeIn, Text = GetLang(TitleText, player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "TITLE");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = LineX, AnchorMax = LineY },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.Lines) }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "LINE");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = PanelX, AnchorMax = PanelY },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "BLOCK_PANEL");

            #region Centering
            Int32 ItemCount = 0;
            Single itemMinPosition = 219f;
            Single itemWidth = 0.2f; /// Ширина
            Single itemMargin = 0.062f; /// Расстояние между 
            Int32 itemCount = CategoryList.Count;
            Single itemMinHeight = 0.87f; // Сдвиг по вертикали
            Single itemHeight = 0.11f; /// Высота
            Int32 ItemTarget = 4;

            if (itemCount > ItemTarget)
            {
                itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                itemCount -= ItemTarget;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;

            #endregion

            Int32 Count = 0;
            Int32 Items = CategoryList.Count;
            Boolean SkipElement = (Boolean)(CategoryList.Count(x => TimeUnblock(x.Value.TimeBlock) > 0) >= 1);

            foreach (var Item in CategoryList.OrderBy(i => i.Value.TimeBlock).Take(28))
            {
                String ColorBackground = SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0 ? Interface.NextBlockedPanel : TimeUnblock(Item.Value.TimeBlock) > 0 ? Interface.BlockedPanel : Item.Value.BlockMoreList.Count > 0 && TimeUnblock(Item.Value.BlockMoreList.Sum(i => Block.VaribleUnlockMore ? (i.Value + Item.Value.TimeBlock) : i.Value)) > 0 ? Interface.UnblockedMorePanel : Interface.UnblockedPanel;
                String LineColor = TimeUnblock(Item.Value.TimeBlock) > 0 ? Interface.Lines : Item.Value.BlockMoreList.Count > 0 && TimeUnblock(Item.Value.BlockMoreList.Sum(i => Block.VaribleUnlockMore ? (i.Value + Item.Value.TimeBlock) : i.Value)) > 0 ? Interface.PreBlockedMoreLine : Interface.LinesUnblock;
                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT + "BLOCK_PANEL",
                    Name = IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}", OffsetMax = "0 1", OffsetMin = "0 1" },
                        new CuiOutlineComponent { Color = HexToRustFormat(LineColor), Distance = "-1.35 1.35", UseGraphicAlpha = true }
                    }
                });

                if (SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0)
                {
                    if (Interface.UseProgressiveBackground)
                    {
                        Single ProgressiveX = (Single)((Item.Value.TimeBlock - TimeUnblock(Item.Value.TimeBlock)) / Item.Value.TimeBlock * 1f) >= 1 ? 1 : (Single)((Item.Value.TimeBlock - TimeUnblock(Item.Value.TimeBlock)) / Item.Value.TimeBlock * 1f);
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{ProgressiveX} 1", OffsetMin = "0 0", OffsetMax = "0 -0.03" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.UnblockedPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}");
                    }
                }

                String PNG = Item.Value.SkinID == 0 ? GetImage($"{Item.Key}_128px") : GetImage($"{Item.Key}_128px_{Item.Value.SkinID}", Item.Value.SkinID);
                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}",
                    Name = IQWIPE_PARENT + $"IMG_{ItemCount}",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = PNG },
                        new CuiRectTransformComponent{ AnchorMin = "0.03992017 0.04031476", AnchorMax = $"0.9481039 0.9574757"},
                    }
                });

                if (!config.GeneralSetting.UseShowAllTime)
                {
                    if (SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0)
                    {
                        String TimeLeft = FormatTime(TimeSpan.FromSeconds(TimeUnblock(Item.Value.TimeBlock)), player.UserIDString);
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.06480032 0.7686631", AnchorMax = "0.9430439 0.980315" },
                            Text = { FadeIn = FadeIn, Text = TimeLeft, FontSize = 12, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + "UNLOCK_TIME" + itemCount);
                    }
                }
                else
                {
                    if (TimeUnblock(Item.Value.TimeBlock) > 0)
                    {
                        String TimeLeft = FormatTime(TimeSpan.FromSeconds(TimeUnblock(Item.Value.TimeBlock)), player.UserIDString);
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.06480032 0.7686631", AnchorMax = "0.9430439 0.980315" },
                            Text = { FadeIn = FadeIn, Text = TimeLeft, FontSize = 12, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + "UNLOCK_TIME" + itemCount);
                    }
                }

                #region Blocked Controller

                if (!SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 -1", OffsetMax = "0 -1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BlurBlockedPanel), Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, IQWIPE_PARENT + $"IMG_{ItemCount}", IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLUR");

                    container.Add(new CuiElement
                    {
                        Parent = IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLUR",
                        Name = IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLOCKED",
                        Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Interface.SpriteBlocked, Color = HexToRustFormat(Interface.Labels) },
                        new CuiRectTransformComponent{ AnchorMin = "0.33 0.3399995", AnchorMax = $"0.65 0.6599998"},
                    }
                    });
                }
                else if (Item.Value.BlockMoreList != null && Item.Value.BlockMoreList.Count != 0 && TimeUnblock(Item.Value.BlockMoreList.Sum(i => Block.VaribleUnlockMore ? (i.Value + Item.Value.TimeBlock) : i.Value)) > 0)
                {
                    container.Add(new CuiButton
                    {
                        FadeOut = 0.2f,
                        RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                        Button = { Command = $"iqwb popup {Item.Key}", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + "BUTTON");
                }

                #endregion

                Count++;

                #region Lines

                if (Count != 4 && Items - Count != 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "1.5 -1", OffsetMax = $"20 1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.Lines) }
                    }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}");

                    if (TimeUnblock(Item.Value.TimeBlock) < 0 || (SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0))
                    {
                        Single ProgressiveX = (Single)((Item.Value.TimeBlock - TimeUnblock(Item.Value.TimeBlock)) / Item.Value.TimeBlock * 1f) >= 1 ? 1 : (Single)((Item.Value.TimeBlock - TimeUnblock(Item.Value.TimeBlock)) / Item.Value.TimeBlock * 1f);
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{ProgressiveX - 0.04} 1", OffsetMin = "0 0", OffsetMax = "0 -0.1" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.LinesUnblock) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "PROGRESSIVE");
                    }
                }
                if (SkipElement && TimeUnblock(Item.Value.TimeBlock) > 0)
                    SkipElement = false;
                if (Count == 4)
                {
                    Items -= 4;
                    if (Items != 0)
                    {
                        Int32 SizeLine = Items == 1 ? -131 : Items == 2 ? -161 : Items == 3 ? -220 : -243; // 

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-1 -10", OffsetMax = "1 0" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{SizeLine} -1", OffsetMax = "2 1" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "-1 -20", OffsetMax = "1 2" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT" + "DOWN");

                    }
                    Count = 0;
                }
                #endregion

                #region Centering
                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % ItemTarget == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 0.5f));
                    if (itemCount > ItemTarget)
                    {
                        itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                        itemCount -= ItemTarget;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
                #endregion
            }
        }

        #endregion

        #region Category More Blocked Popup

        void UI_Category_More_Popup(BasePlayer player, Configuration.Blocks.BlockElement BlockElement)
        {
            CuiHelper.DestroyUi(player, IQWIPE_PARENT + "POPUP");
            CuiElementContainer container = new CuiElementContainer();
            Single FadeIn = 0.2f;
            var Interface = config.Interface;
            var BlockMore = BlockElement.BlockMoreList;
            var Block = config.Block;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BackgroundMoreBlurColor), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, IQWIPE_PARENT, IQWIPE_PARENT + "POPUP");

            container.Add(new CuiButton
            {
                FadeOut = 0.2f,
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { Close = IQWIPE_PARENT + "POPUP", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, IQWIPE_PARENT + "POPUP", IQWIPE_PARENT + "POPUP" + "BUTTON_CLOSE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3281249 0.67961939", AnchorMax = "0.69271939 0.7268518" },
                Text = { FadeIn = FadeIn, Text = GetLang("TITLE_BLOCK_MORE", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQWIPE_PARENT + "POPUP", IQWIPE_PARENT + "POPUP_TITLE");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.4036451 0.6824123", AnchorMax = "0.5916615 0.6829678" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.Lines) }
            }, IQWIPE_PARENT + "POPUP", IQWIPE_PARENT + "POPUP_TITLE_LINE");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.2432292 0.06944448", AnchorMax = "0.7546874 0.6583334" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, IQWIPE_PARENT + "POPUP", IQWIPE_PARENT + "POPUP" + "ITEMS_PANEL");

            #region Centering
            Int32 ItemCount = 0;
            Single itemMinPosition = 219f;
            Single itemWidth = 0.1f; /// Ширина
            Single itemMargin = 0.062f; /// Расстояние между 
            Int32 itemCount = BlockMore.Count;
            Single itemMinHeight = 0.85f; // Сдвиг по вертикали
            Single itemHeight = 0.15f; /// Высота
            Int32 ItemTarget = 4;

            if (itemCount > ItemTarget)
            {
                itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                itemCount -= ItemTarget;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;

            #endregion

            Int32 Count = 0;
            Int32 Items = BlockMore.Count;
            Boolean SkipElement = (Boolean)(BlockMore.Count(x => TimeUnblock(Block.VaribleUnlockMore ? x.Value + BlockElement.TimeBlock : x.Value) > 0) >= 1);

            foreach (var Item in BlockMore.OrderBy(i => i.Value).Take(28))
            {
                Int32 ItemValue = Block.VaribleUnlockMore ? Item.Value + BlockElement.TimeBlock : Item.Value;
                String ColorBackground = SkipElement && TimeUnblock(ItemValue) > 0 ? Interface.NextBlockedPanel : TimeUnblock(ItemValue) > 0 ? Interface.BlockedPanel : Interface.UnblockedPanel;
                String LineColor = TimeUnblock(ItemValue) > 0 ? Interface.Lines : Interface.LinesUnblock;

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT + "POPUP" + "ITEMS_PANEL",
                    Name = IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{ AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}", OffsetMax = "0 1", OffsetMin = "0 1" },
                        new CuiOutlineComponent { Color = HexToRustFormat(LineColor), Distance = "-1.35 1.35", UseGraphicAlpha = true }
                    }
                });

                if (SkipElement && TimeUnblock(ItemValue) > 0)
                {
                    if (Interface.UseProgressiveBackground)
                    {
                        Single ProgressiveX = (Single)((ItemValue - TimeUnblock(ItemValue)) / ItemValue * 1f) >= 1 ? 1 : (Single)((ItemValue - TimeUnblock(ItemValue)) / ItemValue * 1f);
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{ProgressiveX} 1", OffsetMin = "0 0", OffsetMax = "0 -0.03" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.UnblockedPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}");
                    }
                }

                container.Add(new CuiElement
                {
                    Parent = IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}",
                    Name = IQWIPE_PARENT + $"IMG_{ItemCount}",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"{Item.Key}_128px") },
                        new CuiRectTransformComponent{ AnchorMin = "0.03992017 0.04031476", AnchorMax = $"0.9481039 0.9574757"},
                    }
                });

                if (!config.GeneralSetting.UseShowAllTime)
                {
                    if (SkipElement && TimeUnblock(ItemValue) > 0)
                    {
                        String TimeLeft = FormatTime(TimeSpan.FromSeconds(TimeUnblock(ItemValue)), player.UserIDString);
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.06480032 0.7686631", AnchorMax = "0.9430439 0.980315" },
                            Text = { FadeIn = FadeIn, Text = TimeLeft, FontSize = 12, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + "UNLOCK_TIME" + itemCount);
                    }
                }
                else
                {
                    if (TimeUnblock(ItemValue) > 0)
                    {
                        String TimeLeft = FormatTime(TimeSpan.FromSeconds(TimeUnblock(ItemValue)), player.UserIDString);
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.06480032 0.7686631", AnchorMax = "0.9430439 0.980315" },
                            Text = { FadeIn = FadeIn, Text = TimeLeft, FontSize = 12, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + "UNLOCK_TIME" + itemCount);
                    }
                }

                #region Blocked Controller

                if (!SkipElement && TimeUnblock(ItemValue) > 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 -1", OffsetMax = "0 -1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.BlurBlockedPanel), Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, IQWIPE_PARENT + $"IMG_{ItemCount}", IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLUR");

                    container.Add(new CuiElement
                    {
                        Parent = IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLUR",
                        Name = IQWIPE_PARENT + $"IMG_{ItemCount}" + "BLOCKED",
                        Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Interface.SpriteBlocked, Color = HexToRustFormat(Interface.Labels) },
                        new CuiRectTransformComponent{ AnchorMin = "0.33 0.3399995", AnchorMax = $"0.65 0.6599998"},
                    }
                    });
                }

                #endregion

                Count++;

                #region Lines

                if (Count != 4 && Items - Count != 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "1.5 -1", OffsetMax = $"41 1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.Lines) }
                    }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}");

                    if (TimeUnblock(ItemValue) < 0 || (SkipElement && TimeUnblock(ItemValue) > 0))
                    {
                        Single ProgressiveX = (Single)((ItemValue - TimeUnblock(ItemValue)) / ItemValue * 1f) >= 1 ? 1 : (Single)((ItemValue - TimeUnblock(ItemValue)) / ItemValue * 1f);
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{ProgressiveX - 0.04} 1", OffsetMin = "0 0", OffsetMax = "0 -0.1" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.LinesUnblock) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "PROGRESSIVE");
                    }
                }
                if (SkipElement && TimeUnblock(ItemValue) > 0)
                    SkipElement = false;
                if (Count == 4)
                {
                    Items -= 4;
                    if (Items != 0)
                    {
                        Int32 SizeLine = Items == 1 ? -131 : Items == 2 ? -215 : Items == 3 ? -260 : -293; // 

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-1 -10", OffsetMax = "1 0" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{SizeLine} -1", OffsetMax = "2 1" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "-1 -7", OffsetMax = "1 2" },
                            Image = { FadeIn = FadeIn, Color = HexToRustFormat(LineColor) }
                        }, IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT", IQWIPE_PARENT + $"PANEL_ITEM_{ItemCount}" + $"LINE_{ItemCount}" + "LEFT" + "DOWN");

                    }
                    Count = 0;
                }
                #endregion

                #region Centering
                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % ItemTarget == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 0.7f));
                    if (itemCount > ItemTarget)
                    {
                        itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                        itemCount -= ItemTarget;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
                #endregion
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Interface Button Panel

        void Interface_Button_Panel(BasePlayer player)
        {
            var Interface = config.Interface;
            var General = config.GeneralSetting;
            if (!General.UsePanelButton) return;
            if (BlockHideInfo[player.userID]) return;
            if (config.Interface.ButtonIsBlock)
            {
                Int64 TimeBlockMain = config.Block.BlockWeaponAndTools.Union(config.Block.BlockArmory).Union(config.Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value).Sum(s => s.Value.TimeBlock);
                Int64 TimeBlockOther = config.Block.BlockWeaponAndTools.Union(config.Block.BlockArmory).Union(config.Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value).Sum(s => s.Value.BlockMoreList.Sum(z => z.Value));

                Int32 AllTime = Convert.ToInt32(TimeBlockMain + TimeBlockOther);
                if (TimeUnblock(AllTime) <= 0)
                    return;
            }

            CuiHelper.DestroyUi(player, IQWIPE_PARENT_PANEL_BTN);
            CuiElementContainer container = new CuiElementContainer();
            Single FadeIn = 0.2f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-380 -70", OffsetMax = "-10 -10" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, "Hud", IQWIPE_PARENT_PANEL_BTN);

            container.Add(new CuiButton
            {
                FadeOut = 0.2f,
                RectTransform = { AnchorMin = $"0.1469697 0.6190471", AnchorMax = $"1 1" },
                Button = { Command = "block", Color = "0 0 0 0" },
                Text = { Text = GetLang("UI_PANEL_BUTTON_TITLE", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Align = TextAnchor.MiddleRight }
            }, IQWIPE_PARENT_PANEL_BTN, IQWIPE_PARENT_PANEL_BTN + "TITLE_ONE");

            container.Add(new CuiButton
            {
                FadeOut = 0.2f,
                RectTransform = { AnchorMin = $"0.2075757 0.3444443", AnchorMax = $"1 0.626667" },
                Button = { Command = "block", Color = "0 0 0 0" },
                Text = { Text = GetLang("UI_PANEL_BUTTON_DESCRIPTION", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Align = TextAnchor.MiddleRight }
            }, IQWIPE_PARENT_PANEL_BTN, IQWIPE_PARENT_PANEL_BTN + "TITLE_TWO");

            if (General.UseHidePanelButton)
            {
                container.Add(new CuiButton
                {
                    FadeOut = 0.2f,
                    RectTransform = { AnchorMin = $"0.7 0.06666651", AnchorMax = $"1 0.3488889" },
                    Button = { Command = "iqwb hide.panel.btn false", Color = "0 0 0 0" },
                    Text = { Text = GetLang("UI_PANEL_BUTTON_HIDE", player.UserIDString), Color = HexToRustFormat(Interface.Labels), Align = TextAnchor.MiddleRight }
                }, IQWIPE_PARENT_PANEL_BTN, IQWIPE_PARENT_PANEL_BTN + "TITLE_THREE");
            }
            container.Add(new CuiElement
            {
                Parent = IQWIPE_PARENT_PANEL_BTN,
                Name = IQWIPE_PARENT_PANEL_BTN + "ICO",
                Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Interface.SpriteBlockLogo, Color = HexToRustFormat(Interface.Labels) },
                        new CuiRectTransformComponent{ AnchorMin = "0.05896809 0", AnchorMax = $"0.2211303 1"},
                    }
            });

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Interface Alert Block

        Boolean Block_Skin_Controller(Configuration.Blocks.BlockElement BlockInfo, Dictionary<String, Configuration.Blocks.BlockElement> BlockAll, Boolean BlockStatusShortname, UInt64 SkinID = 0)
        {
            if (BlockStatusShortname && SkinID != 0 && SkinID == BlockInfo.SkinID)
                return true;
            else if (BlockInfo.SkinID == 0 && SkinID == 0 && BlockStatusShortname)
                return true;
            else if (BlockStatusShortname && BlockInfo.SkinID == 0 && BlockAll.Count(x => x.Value.SkinID == SkinID) == 0) return true;
            else return false;

        }
        Boolean Interface_UI_Block(BasePlayer player, String Shortname, String ShortnameModule = "", UInt64 SkinID = 0, List<Connection> playerList = null)
        {
            if (String.IsNullOrWhiteSpace(Shortname)) return true;
            if(Shortname.Contains(".ice"))
                Shortname = Shortname.Replace(".ice", "");

            if (playerList == null)
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "ALERT_UI_BLOCK");
                    if (permission.UserHasPermission(player.UserIDString, PermissionIgnoreBlock)) return true;
                }
            }
            else CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "DestroyUI", "ALERT_UI_BLOCK");
            String LangID = player == null ? null : player.UserIDString;
            Single FadeIn = 0f;
            Single FadeOut = 0f;
            var Interface = config.Interface;
            var Block = config.Block;
            Dictionary<String, Configuration.Blocks.BlockElement> BlockAll = (Dictionary<String, Configuration.Blocks.BlockElement>)Block.BlockArmory.Concat(Block.BlockWeaponAndTools).Concat(Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value);
            if (!BlockAll.ContainsKey(Shortname)) return true;
            if (!String.IsNullOrWhiteSpace(ShortnameModule) && !BlockAll[Shortname].BlockMoreList.ContainsKey(ShortnameModule)) return true;

            String ColorBackground = Block_Skin_Controller(BlockAll[Shortname], BlockAll, TimeUnblock(BlockAll[Shortname].TimeBlock) > 0, SkinID) 
                                   ? Interface.BlockedPanel : BlockAll[Shortname].BlockMoreList.Count > 0 && !String.IsNullOrWhiteSpace(ShortnameModule) && TimeUnblock(Block.VaribleUnlockMore 
                                   ? BlockAll[Shortname].TimeBlock + BlockAll[Shortname].BlockMoreList[ShortnameModule] : BlockAll[Shortname].BlockMoreList[ShortnameModule]) > 0 
                                   ? Interface.UnblockedMorePanel : "";

            String LineColor = Block_Skin_Controller(BlockAll[Shortname], BlockAll, TimeUnblock(BlockAll[Shortname].TimeBlock) > 0, SkinID) ? Interface.Lines : BlockAll[Shortname].BlockMoreList.Count > 0 && !String.IsNullOrWhiteSpace(ShortnameModule) && TimeUnblock(Block.VaribleUnlockMore ? BlockAll[Shortname].TimeBlock + BlockAll[Shortname].BlockMoreList[ShortnameModule] : BlockAll[Shortname].BlockMoreList[ShortnameModule]) > 0 ? Interface.PreBlockedMoreLine : "";
            String LangVaribles = Block_Skin_Controller(BlockAll[Shortname], BlockAll, TimeUnblock(BlockAll[Shortname].TimeBlock) > 0, SkinID) ? GetLang("UI_BLOCK_DESCRIPTION_TWO", LangID, FormatTime(TimeSpan.FromSeconds(TimeUnblock(BlockAll[Shortname].TimeBlock)), LangID ?? "0")) : BlockAll[Shortname].BlockMoreList.Count > 0 && !String.IsNullOrWhiteSpace(ShortnameModule) && TimeUnblock(Block.VaribleUnlockMore ? BlockAll[Shortname].TimeBlock + BlockAll[Shortname].BlockMoreList[ShortnameModule] : BlockAll[Shortname].BlockMoreList[ShortnameModule]) > 0 && !String.IsNullOrEmpty(ShortnameModule) ? GetLang("UI_BLOCK_DESCRIPTION_ONE", LangID, FormatTime(TimeSpan.FromSeconds(TimeUnblock(BlockAll[Shortname].BlockMoreList[ShortnameModule])), LangID ?? "0")) : "";
            if (String.IsNullOrWhiteSpace(ColorBackground) || String.IsNullOrWhiteSpace(LineColor) || String.IsNullOrWhiteSpace(LangVaribles)) return true;

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = "Overlay",
                Name = "ALERT_UI_BLOCK",
                Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        new CuiRectTransformComponent{  AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-199 -265", OffsetMax = "180 -225"  },
                        new CuiOutlineComponent { Color = HexToRustFormat(LineColor), Distance = "-1.32 1.32", UseGraphicAlpha = true }
                    }
            });

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.1125769 0.4749999", AnchorMax = "1 0.9833333" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_BLOCK_TITLE", LangID), FontSize = 18, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, "ALERT_UI_BLOCK", "ALERT_UI_BLOCK_TITLE_ONE");

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.1125769 0.04166643", AnchorMax = "1 0.5499999" },
                Text = { FadeIn = FadeIn, Text = LangVaribles, FontSize = 12, Color = HexToRustFormat(Interface.Labels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, "ALERT_UI_BLOCK", "ALERT_UI_BLOCK_TITLE_TWO");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.2f,
                Parent = "ALERT_UI_BLOCK",
                Name = "ALERT_UI_BLOCK_SPRITE",
                Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn - 0.8f, Sprite = "assets/icons/connection.png", Color = HexToRustFormat(Interface.Labels) },
                        new CuiRectTransformComponent{ AnchorMin = "0 1", AnchorMax = $"0 1", OffsetMin = "-20 -48", OffsetMax = "40 10" },
                    }
            });

            if (playerList == null)
            {
                if (player != null)
                    CuiHelper.AddUi(player, container);
                
                timer.Once(3f, () =>
                {
                    if (player != null)
                    {
                        CuiHelper.DestroyUi(player, "ALERT_UI_BLOCK");
                        CuiHelper.DestroyUi(player, "ALERT_UI_BLOCK_TITLE_ONE");
                        CuiHelper.DestroyUi(player, "ALERT_UI_BLOCK_TITLE_TWO");
                        CuiHelper.DestroyUi(player, "ALERT_UI_BLOCK_SPRITE");
                    }
                });
            }
            else
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "AddUI", container.ToJson());
                timer.Once(3f, () =>
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "DestroyUI", "ALERT_UI_BLOCK");
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "DestroyUI", "ALERT_UI_BLOCK_TITLE_ONE");
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "DestroyUI", "ALERT_UI_BLOCK_TITLE_TWO");
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(playerList), null, "DestroyUI", "ALERT_UI_BLOCK_SPRITE");
                });
            }

            return false;
        }

        #endregion

        #region Help
        private static string HexToRustFormat(String hex)
        {
            UnityEngine.Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return String.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion

        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            ServerMgr.Instance.StartCoroutine(DownloadImages());

            ServerMgr.Instance.InvokeRepeating(CheckAllUnlock, 0f, 60f);

            NextTick(() =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player);
            });
        }
        private void CheckAllUnlock()
        {
            Double TimeBlockMain = config.Block.BlockWeaponAndTools.Union(config.Block.BlockArmory).Union(config.Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value).Where(x => TimeUnblock(x.Value.TimeBlock) > 0).Sum(s => TimeUnblock(s.Value.TimeBlock));
            Double TimeBlockOther = config.Block.BlockWeaponAndTools.Union(config.Block.BlockArmory).Union(config.Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value).Sum(s => s.Value.BlockMoreList.Where(x => TimeUnblock(x.Value) > 0).Sum(z => TimeUnblock(z.Value)));

            Double AllTime = TimeBlockMain + TimeBlockOther;
            
            if (AllTime <= 0)
            {
                Unsubscribe("OnPlayerLootEnd");
                //Unsubscribe("CanAcceptItem"); //// TODO
                //Unsubscribe("OnItemAddedToContainer"); //// TODO
                Unsubscribe("CanWearItem");
                Unsubscribe("OnItemAction");
                Unsubscribe("CanEquipItem");
                Unsubscribe("OnReloadWeapon");
                Unsubscribe("OnReloadMagazine");

                if (config.Interface.ButtonIsBlock)
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        CuiHelper.DestroyUi(player, IQWIPE_PARENT_PANEL_BTN);
                }

                if (config.GeneralSetting.AlertAllUsersUnlocked)
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        SendChat(GetLang("CHAT_ALERT_ALL_USERS_ALL_UNLOCK", player.UserIDString), player);

                IsUnlockedAll = true;
                PrintWarning(LanguageEn ? "All items have been successfully unlocked!" : "Все предметы были успешно разблокированы!");
                ServerMgr.Instance.CancelInvoke(CheckAllUnlock);
            }
        }
        private void Init()
        {
            ReadData();
            permission.RegisterPermission(PermissionIgnoreBlock, this);
        }
        void Unload()
        {
            WriteData();
            ServerMgr.Instance.StopCoroutine(DownloadImages());
            ServerMgr.Instance.CancelInvoke(CheckAllUnlock);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, IQWIPE_PARENT);
                CuiHelper.DestroyUi(player, IQWIPE_PARENT_PANEL_BTN);
            }
        }
        void OnPlayerConnected(BasePlayer player)
        {
            RegisteredUser(player);
            CachingImage(player);

            if (config.Interface.ButtonIsBlock)
            {
                if (!IsUnlockedAll)
                    Interface_Button_Panel(player);
                else if (config.GeneralSetting.AlertConnectedUserUnlocked)
                    SendChat(GetLang("CHAT_ALERT_ALL_USERS_ALL_UNLOCK", player.UserIDString), player);
            }
            else
            {
                Interface_Button_Panel(player);
                if (config.GeneralSetting.AlertConnectedUserUnlocked && IsUnlockedAll)
                    SendChat(GetLang("CHAT_ALERT_ALL_USERS_ALL_UNLOCK", player.UserIDString), player);
            }
        }
        public Dictionary<UInt64, List<Connection>> ContainerGetPlayer = new Dictionary<UInt64, List<Connection>>();
        void NetworkIDGetSet(UInt64 NetID, Boolean SetOrRemove, BasePlayer player = null)
        {
            if (player == null) return;
            if (!ContainerGetPlayer.ContainsKey(NetID))
                ContainerGetPlayer.Add(NetID, new List<Connection> { player.Connection });
            else
            {
                if (SetOrRemove)
                {
                    if (!ContainerGetPlayer[NetID].Contains(player.Connection))
                        ContainerGetPlayer[NetID].Add(player.Connection);
                }
                else
                {
                    if (ContainerGetPlayer[NetID].Count <= 1)
                        ContainerGetPlayer.Remove(NetID);
                    else ContainerGetPlayer[NetID].Remove(player.Connection);
                }
            }
        }

        object OnLootNetworkUpdate(PlayerLoot loot)
        {
            if (loot == null)
                return null;
            BasePlayer player = loot.GetComponent<BasePlayer>();
            if (player == null)
                return null;
            if (loot.entitySource == null || loot.entitySource.net == null)
                return null;
            UInt64 NetID = loot.entitySource.net.ID;
            NetworkIDGetSet(NetID, true, player);
            return null;
        }

        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (inventory?.entitySource == null || inventory?.entitySource?.net == null)
                return;
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return;

            UInt64 NetID = inventory.entitySource.net.ID;
            NetworkIDGetSet(NetID, false, player);
        }
        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (item == null || item.info == null || container == null) return null;
            if (container != null && container.availableSlots.Count != 0)
            {
                if (item.parent != null && item.parent.entityOwner != null)
                {
                    UInt64 NetID = item.parent.entityOwner.net.ID;

                    if (ContainerGetPlayer.ContainsKey(NetID))
                    {
                        if (Interface_UI_Block(null, container.parent.info.shortname, item.info.shortname, 0, ContainerGetPlayer[NetID]))
                            return null;
                        else return ItemContainer.CanAcceptResult.CannotAccept;
                    }
                }

                BasePlayer ContainerPlayer = item.GetRootContainer() == null ? null : item.GetRootContainer().playerOwner;
                if (ContainerPlayer != null && ContainerPlayer.GetComponent<NPCPlayer>() == null && ContainerPlayer.GetComponent<BaseNpc>() == null && !ContainerPlayer.IsNpc)
                {
                    if (IsDuel(ContainerPlayer.userID))
                        return null;

                    if (Interface_UI_Block(ContainerPlayer, container.parent.info.shortname, item.info.shortname))
                        return null;
                    else return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null || player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            if (container.availableSlots.Count != 0)
            {
                if (IsDuel(player.userID))
                    return null;

                if (Interface_UI_Block(player, container.parent.info.shortname, item.info.shortname))
                    return null;
                else return ItemContainer.CanAcceptResult.CannotAccept;
            }

            if (container?.entityOwner is AutoTurret)
            {
                if (IsDuel(player.userID))
                    return null;

                if (Interface_UI_Block(player, item.info.shortname, "", item.skin))
                {
                    AutoTurret Turret = container?.entityOwner as AutoTurret;
                    if (Turret == null) return null;
                    if (Turret.inventory.itemList.Count == 0 || Turret.inventory.itemList[0] == null) return null;
                    if (Interface_UI_Block(player, Turret.inventory.itemList[0].info.shortname, item.info.shortname))
                        return null;
                    else return ItemContainer.CanAcceptResult.CannotAccept;
                }
                else return ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null) return;
            BasePlayer player = container.playerOwner;
            if (player == null || !player.userID.IsSteamId() || player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return;
            if (IsDuel(player.userID))
                return;

            if (Interface_UI_Block(player, item?.info?.shortname, "", item.skin))
                SetFlagItem(item, false);
            else
                SetFlagItem(item, true);
        }
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || player == null || String.IsNullOrWhiteSpace(action)) return null;

            if (action == "drop")
                SetFlagItem(item, false);
            return null;
        }
        private bool? CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null || !player.userID.IsSteamId() || player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            if (IsDuel(player.userID))
                return null;

            if (!Interface_UI_Block(player, item.info.shortname, "", item.skin))
                return false;
            else return null;
        }

        private bool? CanEquipItem(PlayerInventory inventory, Item item) => CanWearItem(inventory, item);

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile) => OnReloadMagazine(player, projectile);

        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player == null || player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            if (IsDuel(player.userID))
                return null;

            projectile.primaryMagazine.SwitchAmmoTypesIfNeeded(player);
            projectile.SendNetworkUpdate();

            if (Interface_UI_Block(player, projectile.primaryMagazine.ammoType.shortname))
                if (Interface_UI_Block(player, player.GetActiveItem().info.shortname, projectile.primaryMagazine.ammoType.shortname))
                    return null;
                else return false;
            else return false;
        }
        #endregion

        #region Commands
        [ConsoleCommand("skip.wb")]
        void ConsoleSkipWipeBlock(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin) return;
            if (!arg.HasArgs(1)) return;

            Int32 SkipTime = 0;
            if(!Int32.TryParse(arg.Args[0], out SkipTime))
            {
                PrintWarning(LanguageEn ? "Syntax error! Syntax : skip.wb Time" :  "Ошибка синтаксиса! Синтаксис : skip.wb Time");
                return;
            }
            if(SkipTime < 0)
            {
                PrintWarning(LanguageEn ? "The time cannot be lower than 0!" : "Время не может быть ниже 0!");
                return;
            }
            SkipTimeBlocked = SkipTime;
            PrintWarning(LanguageEn ? $"You have successfully set the time {SkipTime}" : $"Вы успешно установили время {SkipTime}");
        }

        [ChatCommand("block")]
        void ChatCommandBlock(BasePlayer player)
        {
            if (player == null) return;
            UI_IQ_WipeBlock(player);
        }

        [ConsoleCommand("block")]
        void ConsoleCommandBlock(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            UI_IQ_WipeBlock(player);
        }

        [ChatCommand("bpanel")]
        void ChatCommandBlockPanel(BasePlayer player, string cmd, string[] arg)
        {
            var General = config.GeneralSetting;
            if (!General.UsePanelButton) return;
            HideOrUnHigePanel(player, false);
        }

        [ConsoleCommand("bpanel")]
        void ConsoleCommandBlockPanel(ConsoleSystem.Arg arg)
        {
            var General = config.GeneralSetting;
            if (!General.UsePanelButton) return;
            BasePlayer player = arg.Player();
            if (player == null) return;
            HideOrUnHigePanel(player, false);
        }


        [ConsoleCommand("iqwb")]
        void ConsoleFuncCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            String Action = (String)arg.Args[0];
            switch (Action)
            {
                case "popup":
                    {
                        String Shortname = (String)arg.Args[1];
                        Configuration.Blocks.BlockElement BlockElement = GetBlockElement(Shortname);
                        UI_Category_More_Popup(player, BlockElement);
                        break;
                    }
                case "hide.panel.btn":
                    {
                        Boolean IsMenu = Boolean.Parse(arg.Args[1]);
                        HideOrUnHigePanel(player, IsMenu);
                        break;
                    }
            }
        }

        #endregion

        #region Metods

        #region Loaded Images
        private IEnumerator DownloadImages()
        {
            var Block = config.Block;
            Dictionary<String, Configuration.Blocks.BlockElement> BlockAll = (Dictionary<String, Configuration.Blocks.BlockElement>)Block.BlockArmory.Concat(Block.BlockWeaponAndTools).Concat(Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value);
            Puts("AddImages SkyPlugins.ru...");

            foreach (var Item in BlockAll)
            {
                if (Item.Value.SkinID != 0)
                    if (!HasImage($"{Item.Key}_128px_{Item.Value.SkinID}"))
                        AddImage($"http://api.skyplugins.ru/api/getskin/{Item.Value.SkinID}/128", $"{Item.Key}_128px_{Item.Value.SkinID}", Item.Value.SkinID);

                if (!HasImage($"{Item.Key}_128px"))
                    AddImage($"http://api.skyplugins.ru/api/getimage/{Item.Key}/128", $"{Item.Key}_128px");

                foreach (var ItemMore in Item.Value.BlockMoreList)
                    if (!HasImage($"{ItemMore.Key}_128px"))
                        AddImage($"http://api.skyplugins.ru/api/getimage/{ItemMore.Key}/128", $"{ItemMore.Key}_128px");
            }

            if (!String.IsNullOrWhiteSpace(config.Interface.BackgroundUrl))
                if (!HasImage($"BACKGROUND_{config.Interface.BackgroundUrl}"))
                    AddImage(config.Interface.BackgroundUrl, $"BACKGROUND_{config.Interface.BackgroundUrl}");

            yield return new WaitForSeconds(0.04f);

            Puts("AddImages SkyPlugins.ru - completed..");
        }

        void CachingImage(BasePlayer player)
        {
            var Block = config.Block;
            Dictionary<String, Configuration.Blocks.BlockElement> BlockAll = (Dictionary<String, Configuration.Blocks.BlockElement>)Block.BlockArmory.Concat(Block.BlockWeaponAndTools).Concat(Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value);

            foreach (var Item in BlockAll)
            {
                SendImage(player, $"{Item.Key}_128px");

                foreach (var ItemMore in Item.Value.BlockMoreList)
                    SendImage(player, $"{ItemMore.Key}_128px");

                if (Item.Value.SkinID != 0)
                    SendImage(player, $"{Item.Key}_128px_{Item.Value.SkinID}");
            }
        }
        #endregion

        #region Time Metods

        public Double TimeUnblock(Int32 BlockTime) => (SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + BlockTime) - (CurrentTime + SkipTimeBlocked);
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static Double CurrentTime => Facepunch.Math.Epoch.Current;


        public String FormatTime(TimeSpan time, String UserID)
        {
            Boolean UseFullTime = config.Interface.UseFormatTime;
            String Result = String.Empty;
            String Days = GetLang("TITLE_FORMAT_LOCKED_DAYS", UserID);
            String Hourse = GetLang("TITLE_FORMAT_LOCKED_HOURSE", UserID);
            String Minutes = GetLang("TITLE_FORMAT_LOCKED_MINUTES", UserID);
            String Seconds = GetLang("TITLE_FORMAT_LOCKED_SECONDS", UserID);

            if (!UseFullTime)
            {
                if (time.Seconds != 0)
                    Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)}";

                if (time.Minutes != 0)
                    Result = $"{Format(time.Minutes, Minutes, Minutes, Minutes)}";

                if (time.Hours != 0)
                    Result = $"{Format(time.Hours, Hourse, Hourse, Hourse)}";

                if (time.Days != 0)
                    Result = $"{Format(time.Days, Days, Days, Days)}";
            }
            else
            {
                if (time.Days != 0)
                    Result += $"{Format(time.Days, Days, Days, Days)} ";

                if (time.Hours != 0)
                    Result += $"{Format(time.Hours, Hourse, Hourse, Hourse)} ";

                if (time.Minutes != 0)
                    Result += $"{Format(time.Minutes, Minutes, Minutes, Minutes)} ";
                
                if (time.Days == 0 && time.Hours == 0 && time.Minutes == 0 && time.Seconds != 0)
                    Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)} ";
            }

            return Result;
        }

        private String Format(Int32 units, String form1, String form2, String form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form2}";

            return $"{units}{form3}";
        }

        #endregion

        Configuration.Blocks.BlockElement GetBlockElement(String Shortname)
        {
            var Block = config.Block;
            Dictionary<String, Configuration.Blocks.BlockElement> BlockAll = (Dictionary<String, Configuration.Blocks.BlockElement>)Block.BlockArmory.Concat(Block.BlockWeaponAndTools).Concat(Block.BlockBoom).ToDictionary(x => x.Key, x => x.Value);
            if (!BlockAll.ContainsKey(Shortname))
                return null;

            return BlockAll[Shortname];
        }

        void SetFlagItem(Item item, Boolean FlagStatus)
        {
            var Config = config.GeneralSetting;
            if (!Config.UseFlags || item == null) return;
            if (!FlagStatus && item.flags == global::Item.Flag.None) return;
            global::Item.Flag Flag = Config.TypeLock == TypeLock.Fire ? global::Item.Flag.OnFire : Config.TypeLock == TypeLock.Flash ? global::Item.Flag.Cooking : Config.TypeLock == TypeLock.Locked ? global::Item.Flag.IsLocked : global::Item.Flag.Placeholder;
            if (FlagStatus && item.HasFlag(Flag)) return;
            if (item.flags != Flag)
                item.SetFlag(item.flags, false);
            item.SetFlag(Flag, FlagStatus);
        }

        void HideOrUnHigePanel(BasePlayer player, Boolean IsMenu)
        {
            if (BlockHideInfo[player.userID])
            {
                BlockHideInfo[player.userID] = false;
                if (IsMenu)
                    HideButtonIsMenu(player);
                Interface_Button_Panel(player);
            }
            else
            {
                BlockHideInfo[player.userID] = true;
                if (IsMenu)
                    HideButtonIsMenu(player);
                CuiHelper.DestroyUi(player, IQWIPE_PARENT_PANEL_BTN);
            }
        }

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["TITLE_BLOCK"] = "<size=35><b>BLOCKED WIPE</b></size>",
                ["TITLE_CATEGORY_WEAPON_TOOL"] = "<size=16><b>WEAPONS AND TOOLS</b></size>",
                ["TITLE_CATEGORY_ATTIRE"] = "<size=16><b>EQUIPMENT</b></size>",
                ["TITLE_CATEGORY_BOOM"] = "<size=16><b>EXPLOSIVES AND AMMUNITION</b></size>",
                ["TITLE_BLOCK_MORE"] = "<size=17><b>LOCKING ELEMENTS TO THIS ITEM</b></size>",

                ["TITLE_FORMAT_LOCKED_DAYS"] = "<size=12><b>D</b></size>",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "<size=12><b>H</b></size>",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "<size=12><b>M</b></size>",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "<size=12><b>S</b></size>",
                ["TITLE_BUTTON_CLOSE"] = "<size=35><b>CLOSE</b></size>",

                ["TITLE_INFO_BLOCKED"] = "<size=10><b>- BLOCKED</b></size>",
                ["TITLE_INFO_UNBLOCKED"] = "<size=10><b>- UNBLOCKED</b></size>",
                ["TITLE_INFO_PREBLOCKED"] = "<size=10><b>- IN THE PROCESS OF UNLOCKING</b></size>",
                ["TITLE_INFO_MOREBLOCKED"] = "<size=10><b>- ADDITIONAL BLOCKING</b></size>",

                ["UI_BLOCK_TITLE"] = "<size=18><b>THE ITEM IS TEMPORARILY BLOCKED</b></size>",
                ["UI_BLOCK_DESCRIPTION_ONE"] = "<size=12><b>This item item is temporarily blocked {0}</b></size>",
                ["UI_BLOCK_DESCRIPTION_TWO"] = "<size=12><b>This item is temporarily blocked on {0}</b></size>",

                ["UI_PANEL_BUTTON_TITLE"] = "<size=18><b>TEMPORARY BLOCKING OF ITEMS</b></size>",
                ["UI_PANEL_BUTTON_DESCRIPTION"] = "<size=13>CLICK TO OPEN THE MENU WITH THE LOCK</size>",
                ["UI_PANEL_BUTTON_HIDE"] = "<size=11>HIDE THIS WINDOW</size>",
                ["UI_PANEL_BUTTON_HIDE_ISMENU_OFF"] = "<size=11>HIDE MENU SHORTCUT BUTTONS</size>",
                ["UI_PANEL_BUTTON_HIDE_ISMENU_ON"] = "<size=11>EXPAND THE QUICK ACCESS BUTTONS TO THE MENU</size>",

                ["CHAT_ALERT_ALL_USERS_ALL_UNLOCK"] = "Hurray! All items have been unlocked!",

            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["TITLE_BLOCK"] = "<size=35><b>БЛОКИРОВКА ПОСЛЕ ВАЙПА</b></size>",
                ["TITLE_BLOCK_MORE"] = "<size=17><b>БЛОКИРОВКА ЭЛЕМЕНТОВ К ЭТОМУ ПРЕДМЕТУ</b></size>",
                ["TITLE_CATEGORY_WEAPON_TOOL"] = "<size=16><b>ОРУЖИЕ И ИНСТРУМЕНТЫ</b></size>",
                ["TITLE_CATEGORY_BOOM"] = "<size=16><b>ВЗРЫВЧАТКА И БОЕПРИПАСЫ</b></size>",
                ["TITLE_CATEGORY_ATTIRE"] = "<size=16><b>СНАРЯЖЕНИЕ</b></size>",

                ["TITLE_FORMAT_LOCKED_DAYS"] = "<size=12><b>Д</b></size>",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "<size=12><b>Ч</b></size>",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "<size=12><b>М</b></size>",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "<size=12><b>С</b></size>",
                ["TITLE_BUTTON_CLOSE"] = "<size=35><b>ЗАКРЫТЬ</b></size>",

                ["TITLE_INFO_BLOCKED"] = "<size=10><b>- ЗАБЛОКИРОВАНО</b></size>",
                ["TITLE_INFO_UNBLOCKED"] = "<size=10><b>- РАЗБЛОКИРОВАНО</b></size>",
                ["TITLE_INFO_PREBLOCKED"] = "<size=10><b>- В ПРОЦЕССЕ РАЗБЛОКИРОВКИ</b></size>",
                ["TITLE_INFO_MOREBLOCKED"] = "<size=10><b>- ДОПОЛНИТЕЛЬНАЯ БЛОКИРОВКА</b></size>",

                ["UI_BLOCK_TITLE"] = "<size=18><b>ПРЕДМЕТ ВРЕМЕННО ЗАБЛОКИРОВАН</b></size>",
                ["UI_BLOCK_DESCRIPTION_ONE"] = "<size=12><b>Данный элемент предмета временно заблокирован {0}</b></size>",
                ["UI_BLOCK_DESCRIPTION_TWO"] = "<size=12><b>Данный предмет временно заблокирован на {0}</b></size>",

                ["UI_PANEL_BUTTON_TITLE"] = "<size=18><b>ВРЕМЕННАЯ БЛОКИРОВКА ПРЕДМЕТОВ</b></size>",
                ["UI_PANEL_BUTTON_DESCRIPTION"] = "<size=13>НАЖМИТЕ ЧТОБЫ ОТКРЫТЬ МЕНЮ С БЛОКИРОВКОЙ</size>",
                ["UI_PANEL_BUTTON_HIDE"] = "<size=11>СКРЫТЬ ДАННОЕ ОКНО</size>",
                ["UI_PANEL_BUTTON_HIDE_ISMENU_OFF"] = "<size=11>СКРЫТЬ КНОПКИ БЫСТРОГО ДОСТУПА К МЕНЮ</size>",
                ["UI_PANEL_BUTTON_HIDE_ISMENU_ON"] = "<size=11>РАСКРЫТЬ КНОПКИ БЫСТРОГО ДОСТУПА К МЕНЮ</size>",

                ["CHAT_ALERT_ALL_USERS_ALL_UNLOCK"] = "Ура! Все предметы были разблокированы!",

            }, this, "ru");
            PrintWarning(LanguageEn ? "Language file uploaded successfully" : "Языковой файл загружен успешно");
        }

        public static StringBuilder sb = new StringBuilder();
        public String GetLang(String LangKey, String userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        #endregion
    }
}
