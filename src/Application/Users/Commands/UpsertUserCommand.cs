using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Crpg.Application.Common;
using Crpg.Application.Common.Helpers;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Interfaces.Events;
using Crpg.Application.Common.Mappings;
using Crpg.Application.Steam;
using Crpg.Application.Users.Models;
using Crpg.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Crpg.Application.Users.Commands
{
    public class UpsertUserCommand : IRequest<UserViewModel>, IMapFrom<SteamPlayer>
    {
        public string PlatformId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Uri Avatar { get; set; } = default!;
        public Uri AvatarMedium { get; set; } = default!;
        public Uri AvatarFull { get; set; } = default!;

        public void Mapping(Profile profile)
        {
            profile.CreateMap<SteamPlayer, UpsertUserCommand>()
                .ForMember(u => u.PlatformId, opt => opt.MapFrom(p => p.SteamId))
                .ForMember(u => u.Name, opt => opt.MapFrom(p => p.PersonaName));
        }

        public class Validator : AbstractValidator<UpsertUserCommand>
        {
            public Validator()
            {
                RuleFor(u => u.Name).NotNull().NotEmpty();
                RuleFor(u => u.Avatar).NotNull();
                RuleFor(u => u.AvatarMedium).NotNull();
                RuleFor(u => u.AvatarFull).NotNull();
            }
        }

        public class Handler : IRequestHandler<UpsertUserCommand, UserViewModel>
        {
            private readonly ICrpgDbContext _db;
            private readonly IMapper _mapper;
            private readonly IEventRaiser _events;

            public Handler(ICrpgDbContext db, IMapper mapper, IEventRaiser events)
            {
                _db = db;
                _mapper = mapper;
                _events = events;
            }

            public async Task<UserViewModel> Handle(UpsertUserCommand request, CancellationToken cancellationToken)
            {
                var user =
                    await _db.Users.FirstOrDefaultAsync(u => u.PlatformId == request.PlatformId, cancellationToken)
                    ?? new User { PlatformId = request.PlatformId };

                user.Name = request.Name;
                user.AvatarSmall = request.Avatar;
                user.AvatarMedium = request.AvatarMedium;
                user.AvatarFull = request.AvatarFull;

                if (_db.Entry(user).State == EntityState.Detached)
                {
                    UserHelper.SetDefaultValuesForUser(user);
                    _db.Users.Add(user);
                    _events.Raise(EventLevel.Info, $"{request.Name} joined ({request.PlatformId})", string.Empty, "user_created");
                }

                await _db.SaveChangesAsync(cancellationToken);
                return _mapper.Map<UserViewModel>(user);
            }
        }
    }
}