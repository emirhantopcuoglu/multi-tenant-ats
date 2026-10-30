using Ats.Shared.Kernel;
using MediatR;

namespace Ats.Modules.Notifications.Application;

// Kept per module, like the other modules, so the Kernel doesn't depend on MediatR.
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
