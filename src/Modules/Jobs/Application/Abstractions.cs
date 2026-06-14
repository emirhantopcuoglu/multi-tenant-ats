using Ats.Shared.Kernel;
using MediatR;

namespace Ats.Modules.Jobs.Application;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
