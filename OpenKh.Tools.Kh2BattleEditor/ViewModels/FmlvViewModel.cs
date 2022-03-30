using OpenKh.Kh2;
using OpenKh.Kh2.Battle;
using OpenKh.Tools.Kh2BattleEditor.Extensions;
using OpenKh.Tools.Kh2BattleEditor.Interfaces;
using OpenKh.Tools.Kh2BattleEditor.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.Tools.Wpf.Models;

namespace OpenKh.Tools.Kh2BattleEditor.ViewModels
{
    public class FmlvViewModel : GenericListModel<FmlvFormViewModel>, IBattleGetChanges
    {
        private const string entryName = "fmlv";
        
        public string EntryName => entryName;
        
        public FmlvViewModel() :
            this(new List<Fmlv>())
        {}

        public FmlvViewModel(IEnumerable<Bar.Entry> entries) :
            this(Fmlv.Read(entries.GetBattleStream(entryName)))
        {}


        public FmlvViewModel(IEnumerable<Fmlv> levels) :
            this(levels, (levels.ToList().Count == 0x2D))
        {}

        public FmlvViewModel(IEnumerable<Fmlv> list, bool isFinalMix) :
            base(list.GroupBy(x => x.FormId).Select(x => new FmlvFormViewModel(x, isFinalMix)))
        {}

        public Stream CreateStream()
        {
            var stream = new MemoryStream();
            Fmlv.Write(stream, UnfilteredItems.SelectMany(form => form).Select(x => x.Level).ToList());
            return stream;
        }
    }

    public class FmlvFormViewModel : GenericListModel<FmlvFormViewModel.FmlvLevelViewModel>
    {
        private readonly IGrouping<int, Fmlv> fmlvGroup;
        private readonly bool isFinalMix;

        public FmlvFormViewModel(IGrouping<int, Fmlv> x, bool isFinalMix) :
            base(x.Select(y => new FmlvLevelViewModel(y)))
            
        {
            fmlvGroup = x;
            this.isFinalMix = isFinalMix;
        }

        public string Name => FormNameProvider.GetFormName(fmlvGroup.Key, isFinalMix);

        public class FmlvLevelViewModel
        {
            public Fmlv Level { get; }
            public FmlvLevelViewModel(Fmlv level)
            {
                Level = level;
            }

            public string Name => $"Level {Level.FormLevel}";

            public int AbilityLevel { get => Level.AbilityLevel; set => Level.AbilityLevel = value; }
            public int AntiRate { get => Level.AntiRate; set => Level.AntiRate = value; }
            public ushort Ability { get => Level.Ability; set => Level.Ability = value; }
            public int Exp { get => Level.Exp; set => Level.Exp = value; }
        }
    }
}
