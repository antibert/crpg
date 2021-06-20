﻿using Crpg.Domain.Common;
using Crpg.Domain.Entities.Heroes;
using Crpg.Domain.Entities.Items;

namespace Crpg.Domain.Entities.Settlements
{
    /// <summary>
    /// Item owned by a settlement on Strategus. Similar to <see cref="HeroItem"/> but for <see cref="Settlement"/>s.
    /// </summary>
    public class SettlementItem : AuditableEntity
    {
        public int SettlementId { get; set; }
        public int ItemId { get; set; }
        public int Count { get; set; }

        public Settlement? Settlement { get; set; }
        public Item? Item { get; set; }
    }
}