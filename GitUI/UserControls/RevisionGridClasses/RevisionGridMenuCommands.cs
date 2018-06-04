﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Hotkey;
using JetBrains.Annotations;
using ResourceManager;

namespace GitUI.UserControls.RevisionGridClasses
{
    internal class RevisionGridMenuCommands : MenuCommandsBase
    {
        private readonly TranslationString _quickSearchQuickHelp = new TranslationString("Start typing in revision grid to start quick search.");
        private readonly TranslationString _noRevisionFoundError = new TranslationString("No revision found.");

        private readonly RevisionGrid _revisionGrid;

        // must both be created only once
        private IReadOnlyList<MenuCommand> _navigateMenuCommands;
        private IReadOnlyList<MenuCommand> _viewMenuCommands;

        public RevisionGridMenuCommands(RevisionGrid revisionGrid)
        {
            _revisionGrid = revisionGrid;
            CreateOrUpdateMenuCommands(); // for translation
            TranslationCategoryName = "RevisionGrid";
            Translate();
        }

        /// <summary>
        /// ... "Update" because the hotkey settings might change
        /// </summary>
        public void CreateOrUpdateMenuCommands()
        {
            if (_navigateMenuCommands == null && _viewMenuCommands == null)
            {
                _navigateMenuCommands = CreateNavigateMenuCommands();
                _viewMenuCommands = CreateViewMenuCommands();
            }

            if (_navigateMenuCommands != null && _viewMenuCommands != null)
            {
                var navigateMenuCommands2 = CreateNavigateMenuCommands();
                var viewMenuCommands2 = CreateViewMenuCommands();

                UpdateMenuCommandShortcutKeyDisplayString(_navigateMenuCommands, navigateMenuCommands2);
                UpdateMenuCommandShortcutKeyDisplayString(_viewMenuCommands, viewMenuCommands2);

                if (_revisionGrid != null)
                {
                    // null when TranslationApp is started
                    TriggerMenuChanged(); // trigger refresh
                }
            }
        }

        public void TriggerMenuChanged()
        {
            OnMenuChanged();
        }

        private static void UpdateMenuCommandShortcutKeyDisplayString(IReadOnlyList<MenuCommand> targetList, IEnumerable<MenuCommand> sourceList)
        {
            foreach (var sourceMc in sourceList.Where(mc => !mc.IsSeparator))
            {
                var targetMc = targetList.Single(mc => !mc.IsSeparator && mc.Name == sourceMc.Name);
                targetMc.ShortcutKeyDisplayString = sourceMc.ShortcutKeyDisplayString;
            }
        }

        public IReadOnlyList<MenuCommand> GetNavigateMenuCommands()
        {
            return _navigateMenuCommands;
        }

        private IReadOnlyList<MenuCommand> CreateNavigateMenuCommands()
        {
            return new[]
            {
                new MenuCommand
                {
                    Name = "GotoCurrentRevision",
                    Text = "Go to current revision",
                    Image = Properties.Resources.IconGotoCurrentRevision,
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.SelectCurrentRevision),
                    ExecuteAction = SelectCurrentRevisionExecute
                },
                new MenuCommand
                {
                    Name = "GotoCommit",
                    Text = "Go to commit...",
                    Image = Properties.Resources.IconGotoCommit,
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.GoToCommit),
                    ExecuteAction = GotoCommitExcecute
                },
                MenuCommand.CreateSeparator(),
                new MenuCommand
                {
                    Name = "GotoChildCommit",
                    Text = "Go to child commit",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.GoToChild),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.GoToChild)
                },
                new MenuCommand
                {
                    Name = "GotoParentCommit",
                    Text = "Go to parent commit",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.GoToParent),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.GoToParent)
                },
                MenuCommand.CreateSeparator(),
                new MenuCommand
                {
                    Name = "NavigateBackward",
                    Text = "Navigate backward",
                    ShortcutKeyDisplayString = (Keys.Alt | Keys.Left).ToShortcutKeyDisplayString(),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.NavigateBackward)
                },
                new MenuCommand
                {
                    Name = "NavigateForward",
                    Text = "Navigate forward",
                    ShortcutKeyDisplayString = (Keys.Alt | Keys.Right).ToShortcutKeyDisplayString(),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.NavigateForward)
                },
                MenuCommand.CreateSeparator(),
                new MenuCommand
                {
                    Name = "QuickSearch",
                    Text = "Quick search",
                    ExecuteAction = () => MessageBox.Show(_quickSearchQuickHelp.Text)
                },
                new MenuCommand
                {
                    Name = "PrevQuickSearch",
                    Text = "Quick search previous",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.PrevQuickSearch),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.PrevQuickSearch)
                },
                new MenuCommand
                {
                    Name = "NextQuickSearch",
                    Text = "Quick search next",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.NextQuickSearch),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.NextQuickSearch)
                }
            };
        }

        /// <summary>
        /// this is needed because _revsionGrid is null when TranslationApp is called
        /// </summary>
        [CanBeNull]
        private string GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands revGridCommands)
        {
            return _revisionGrid?.GetShortcutKeys(revGridCommands).ToShortcutKeyDisplayString();
        }

        private IReadOnlyList<MenuCommand> CreateViewMenuCommands()
        {
            return new[]
            {
                // the first three MenuCommands just reuse (the currently rather
                // convoluted) logic from RevisionGrid.
                //
                // After refactoring the three items should be added to RevisionGrid
                // as done with "ShowRemoteBranches" and not via RevisionGrid.Designer.cs
                new MenuCommand
                {
                    Name = "ShowAllBranches",
                    Text = "Show all branches",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ShowAllBranches),
                    ExecuteAction = () => _revisionGrid.ShowAllBranches(),
                    IsCheckedFunc = () => _revisionGrid.IsShowAllBranchesChecked
                },
                new MenuCommand
                {
                    Name = "ShowCurrentBranchOnly",
                    Text = "Show current branch only",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ShowCurrentBranchOnly),
                    ExecuteAction = () => _revisionGrid.ShowCurrentBranchOnly(),
                    IsCheckedFunc = () => _revisionGrid.IsShowCurrentBranchOnlyChecked
                },
                new MenuCommand
                {
                    Name = "ShowFilteredBranches",
                    Text = "Show filtered branches",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ShowFilteredBranches),
                    ExecuteAction = () => _revisionGrid.ShowFilteredBranches(),
                    IsCheckedFunc = () => _revisionGrid.IsShowFilteredBranchesChecked
                },

                MenuCommand.CreateSeparator(),

                new MenuCommand
                {
                    Name = "ShowRemoteBranches",
                    Text = "Show remote branches",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ShowRemoteBranches),
                    ExecuteAction = () => _revisionGrid.ToggleShowRemoteBranches(),
                    IsCheckedFunc = () => AppSettings.ShowRemoteBranches
                },
                new MenuCommand
                {
                    Name = "ShowReflogReferences",
                    Text = "Show reflog references",
                    ExecuteAction = () => _revisionGrid.ToggleShowReflogReferences(),
                    IsCheckedFunc = () => AppSettings.ShowReflogReferences
                },

                MenuCommand.CreateSeparator(),

                new MenuCommand
                {
                    Name = "ShowSuperprojectTags",
                    Text = "Show superproject tags",
                    ExecuteAction = () => _revisionGrid.ToggleShowSuperprojectTags(),
                    IsCheckedFunc = () => AppSettings.ShowSuperprojectTags
                },
                new MenuCommand
                {
                    Name = "ShowSuperprojectBranches",
                    Text = "Show superproject branches",
                    ExecuteAction = () => _revisionGrid.ShowSuperprojectBranches_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowSuperprojectBranches
                },
                new MenuCommand
                {
                    Name = "ShowSuperprojectRemoteBranches",
                    Text = "Show superproject remote branches",
                    ExecuteAction = () => _revisionGrid.ShowSuperprojectRemoteBranches_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowSuperprojectRemoteBranches
                },

                MenuCommand.CreateSeparator(),

                new MenuCommand
                {
                    Name = "showRevisionGraphToolStripMenuItem",
                    Text = "Show revision graph",
                    ExecuteAction = () => _revisionGrid.ToggleRevisionGraph(),
                    IsCheckedFunc = () => _revisionGrid.IsGraphLayout
                },
                new MenuCommand
                {
                    Name = "drawNonrelativesGrayToolStripMenuItem",
                    Text = "Draw non relatives gray",
                    ExecuteAction = () => _revisionGrid.DrawNonrelativesGray_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.RevisionGraphDrawNonRelativesGray
                },
                new MenuCommand
                {
                    Name = "orderRevisionsByDateToolStripMenuItem",
                    Text = "Order revisions by date",
                    ExecuteAction = () => _revisionGrid.ToggleOrderRevisionByDate(),
                    IsCheckedFunc = () => AppSettings.OrderRevisionByDate
                },
                new MenuCommand
                {
                    Name = "showAuthorDateToolStripMenuItem",
                    Text = "Show author date",
                    ExecuteAction = () => _revisionGrid.ToggleShowAuthorDate(),
                    IsCheckedFunc = () => AppSettings.ShowAuthorDate
                },
                new MenuCommand
                {
                    Name = "showRelativeDateToolStripMenuItem",
                    Text = "Show relative date",
                    ExecuteAction = () => _revisionGrid.ShowRelativeDate_ToolStripMenuItemClick(null),
                    IsCheckedFunc = () => AppSettings.RelativeDate
                },
                new MenuCommand
                {
                    Name = "showMergeCommitsToolStripMenuItem",
                    Text = "Show merge commits",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ToggleShowMergeCommits),
                    ExecuteAction = () => _revisionGrid.ShowMergeCommits_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowMergeCommits
                },
                new MenuCommand
                {
                    Name = "showTagsToolStripMenuItem",
                    Text = "Show tags",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ToggleShowTags),
                    ExecuteAction = () => _revisionGrid.ShowTags_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowTags
                },
                new MenuCommand
                {
                    Name = "showIdsToolStripMenuItem",
                    Text = "Show SHA-1",
                    ExecuteAction = () => _revisionGrid.ShowIds_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowIds
                },
                new MenuCommand
                {
                    Name = "showGitNotesToolStripMenuItem",
                    Text = "Show git notes",
                    ExecuteAction = () => _revisionGrid.ShowGitNotes_ToolStripMenuItemClick(),
                    IsCheckedFunc = () => AppSettings.ShowGitNotes
                },
                new MenuCommand
                {
                    Name = "showIsMessageMultilineToolStripMenuItem",
                    Text = "Show indicator for multiline message",
                    ExecuteAction = () =>
                    {
                        AppSettings.ShowIndicatorForMultilineMessage = !AppSettings.ShowIndicatorForMultilineMessage;
                        _revisionGrid.ForceRefreshRevisions();
                    },
                    IsCheckedFunc = () => AppSettings.ShowIndicatorForMultilineMessage
                },

                MenuCommand.CreateSeparator(),

                new MenuCommand
                {
                    Name = "ToggleHighlightSelectedBranch",
                    Text = "Highlight selected branch (until refresh)",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ToggleHighlightSelectedBranch),
                    ExecuteAction = () => _revisionGrid.ExecuteCommand(RevisionGrid.Commands.ToggleHighlightSelectedBranch)
                },
                new MenuCommand
                {
                    Name = "ToggleRevisionCardLayout",
                    Text = "Change commit view layout",
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ToggleRevisionCardLayout),
                    ExecuteAction = () => _revisionGrid.ToggleRevisionCardLayout()
                },

                MenuCommand.CreateSeparator(),

                new MenuCommand
                {
                    Name = "showFirstParent",
                    Text = "Show first parents",
                    Image = Properties.Resources.IconShowFirstParent,
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.ShowFirstParent),
                    ExecuteAction = () => _revisionGrid.ShowFirstParent(),
                    IsCheckedFunc = () => AppSettings.ShowFirstParent
                },
                new MenuCommand
                {
                    Name = "filterToolStripMenuItem",
                    Text = "Set advanced filter",
                    Image = Properties.Resources.IconFilter,
                    ShortcutKeyDisplayString = GetShortcutKeyDisplayStringFromRevisionGridIfAvailable(RevisionGrid.Commands.RevisionFilter),
                    ExecuteAction = () => _revisionGrid.ShowRevisionFilterDialog()
                },
                new MenuCommand
                {
                    Name = "ToggleBranchTreePanel",
                    Text = "Toggle left panel",
                    Image = Properties.MsVsImages.Branch_16x,
                    ExecuteAction = () => _revisionGrid.OnToggleBranchTreePanelRequested()
                }
            };
        }

        public IReadOnlyList<MenuCommand> GetViewMenuCommands()
        {
            return _viewMenuCommands;
        }

        public event EventHandler MenuChanged;

        // taken from http://stackoverflow.com/questions/5058254/inotifypropertychanged-propertychangedeventhandler-event-is-always-null
        // paramenter name not used
        private void OnMenuChanged()
        {
            MenuChanged?.Invoke(this, null);

            foreach (var menuCommand in GetMenuCommandsWithoutSeparators())
            {
                menuCommand.SetCheckForRegisteredMenuItems();
                menuCommand.UpdateMenuItemsShortcutKeyDisplayString();
            }
        }

        protected override IEnumerable<MenuCommand> GetMenuCommandsForTranslation()
        {
            return GetMenuCommandsWithoutSeparators();
        }

        private IEnumerable<MenuCommand> GetMenuCommandsWithoutSeparators()
        {
            return _navigateMenuCommands.Concat(_viewMenuCommands).Where(mc => !mc.IsSeparator);
        }

        private void SelectCurrentRevisionExecute()
        {
            _revisionGrid.ExecuteCommand(RevisionGrid.Commands.SelectCurrentRevision);
        }

        public void GotoCommitExcecute()
        {
            using (var formGoToCommit = new FormGoToCommit(_revisionGrid.UICommands))
            {
                if (formGoToCommit.ShowDialog(_revisionGrid) != DialogResult.OK)
                {
                    return;
                }

                var revisionGuid = formGoToCommit.ValidateAndGetSelectedRevision();
                if (revisionGuid != null)
                {
                    _revisionGrid.SetSelectedRevision(new GitRevision(revisionGuid.ToString()));
                }
                else
                {
                    MessageBox.Show(_revisionGrid, _noRevisionFoundError.Text);
                }
            }
        }
    }
}
