using Ats.Shared.Kernel;
using MediatR;

namespace Ats.Modules.Interviews.Application;

// These marker interfaces mirror the other modules on purpose. Each module owns its own MediatR
// pipeline and registers handlers from its own assembly, so the abstractions are kept per-module
// rather than centralised in the Kernel (which would force a MediatR dependency on every project).
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
