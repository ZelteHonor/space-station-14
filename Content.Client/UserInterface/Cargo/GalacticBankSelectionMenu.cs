﻿
using Content.Client.GameObjects.Components.Cargo;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using System;
using static Robust.Client.UserInterface.Controls.ItemList;

namespace Content.Client.UserInterface.Cargo
{
    public class GalacticBankSelectionMenu : SS14Window
    {
        private ItemList _accounts;
        private int _accountCount = 0;
        private string[] _accountNames = new string[] { };
        private int[] _accountIds = new int[] { };
        private int _selectedAccountId = -1;

#pragma warning disable 649
        [Dependency] private readonly ILocalizationManager _loc;
#pragma warning restore 649

        protected override Vector2? CustomSize => (300, 300);

        public CargoConsoleBoundUserInterface Owner;

        public GalacticBankSelectionMenu()
        {
            IoCManager.InjectDependencies(this);

            Title = _loc.GetString("Galactic Bank Selection");

            _accounts = new ItemList() { SelectMode = ItemList.ItemListSelectMode.Single };

            var margin = new MarginContainer()
            {
                SizeFlagsVertical = SizeFlags.FillExpand,
                SizeFlagsHorizontal = SizeFlags.FillExpand,
                MarginTop = 5f,
                MarginLeft = 5f,
                MarginRight = -5f,
                MarginBottom = -5f,
            };

            margin.AddChild(_accounts);

            Contents.AddChild(margin);
        }

        public void Populate(int accountCount, string[] accountNames, int[] accountIds, int selectedAccountId)
        {
            _accountCount = accountCount;
            _accountNames = accountNames;
            _accountIds = accountIds;
            _selectedAccountId = selectedAccountId;

            _accounts.Clear();
            for (var i = 0; i < _accountCount; i++)
            {
                var id = _accountIds[i];
                _accounts.AddItem($"ID: {id} || {_accountNames[i]}");
                if (id == _selectedAccountId)
                    _accounts[id].Selected = true;
            }
        }
    }
}
