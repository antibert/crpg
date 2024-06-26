using AutoMapper;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Common.Services;
using Crpg.Application.Items.Models;
using Crpg.Domain.Entities.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LoggerFactory = Crpg.Logging.LoggerFactory;

namespace Crpg.Application.Items.Commands;

public record UpgradeUserItemCommand : IMediatorRequest<UserItemViewModel>
{
    public int UserItemId { get; init; }
    public int UserId { get; init; }

    internal class Handler : IMediatorRequestHandler<UpgradeUserItemCommand, UserItemViewModel>
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger<UpgradeUserItemCommand>();

        private readonly ICrpgDbContext _db;
        private readonly IMapper _mapper;
        private readonly IActivityLogService _activityLogService;

        public Handler(ICrpgDbContext db, IMapper mapper, IActivityLogService activityLogService)
        {
            _db = db;
            _mapper = mapper;
            _activityLogService = activityLogService;
        }

        public async Task<Result<UserItemViewModel>> Handle(UpgradeUserItemCommand req, CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .AsSplitQuery()
                .Include(u => u.Items).ThenInclude(ui => ui.Item)
                .FirstOrDefaultAsync(u => u.Id == req.UserId, cancellationToken);

            if (user == null)
            {
                return new(CommonErrors.UserNotFound(req.UserId));
            }

            if (user.HeirloomPoints < 1)
            {
                return new(CommonErrors.NotEnoughHeirloomPoints(1, user.HeirloomPoints));
            }

            var userItemToUpgrade = user.Items
                .FirstOrDefault(ui => ui.Id == req.UserItemId);

            if (userItemToUpgrade == null)
            {
                return new(CommonErrors.UserItemNotFound(req.UserItemId));
            }

            if (userItemToUpgrade.IsBroken)
            {
                return new(CommonErrors.ItemBroken(userItemToUpgrade.ItemId));
            }

            if (userItemToUpgrade.Item!.Type == ItemType.Banner)
            {
                return new(CommonErrors.ItemNotUpgradable(userItemToUpgrade.ItemId));
            }

            Item? upgradedItem = await _db.Items
                .FirstOrDefaultAsync(
                    i => i.BaseId == userItemToUpgrade.Item!.BaseId && i.Rank == userItemToUpgrade.Item!.Rank + 1,
                    cancellationToken);

            if (upgradedItem == null)
            {
                return new(CommonErrors.UserItemMaxRankReached(userItemToUpgrade.Id, userItemToUpgrade.Item!.Rank));
            }

            userItemToUpgrade.Item = upgradedItem;
            user.HeirloomPoints -= 1;

            _db.ActivityLogs.Add(_activityLogService.CreateItemUpgradedLog(user.Id, userItemToUpgrade.ItemId, 1));
            await _db.SaveChangesAsync(cancellationToken);

            Logger.LogInformation("User '{0}' has upgraded item '{1}'", req.UserId, req.UserItemId);
            return new(_mapper.Map<UserItemViewModel>(userItemToUpgrade));
        }
    }
}
