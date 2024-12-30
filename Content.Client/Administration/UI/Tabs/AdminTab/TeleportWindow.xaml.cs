﻿using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Administration.UI.Tabs.AdminTab
{
    [GenerateTypedNameReferences]
    [UsedImplicitly]
    public sealed partial class TeleportWindow : DefaultWindow
    {
        private PlayerInfo? _selectedPlayer;

        protected override void EnteredTree()
        {
            SubmitButton.OnPressed += SubmitButtonOnOnPressed;
            PlayerList.OnSelectionChanged += OnListOnOnSelectionChanged;
        }

        private void OnListOnOnSelectionChanged(PlayerInfo? obj)
        {
            _selectedPlayer = obj;
            SubmitButton.Disabled = _selectedPlayer == null;
        }

        private void SubmitButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            if (_selectedPlayer == null)
                return;
            // Execute command
            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(
                $"tpto \"{_selectedPlayer.Username}\"");
        }
    }
}
