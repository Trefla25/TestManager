using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace eHub.Scripting.Connectors.Db;

internal static class ExpressionMagic
{
    private static readonly MethodInfo UpdateAsyncMethodInfo =
        typeof(EntityFrameworkQueryableExtensions).GetMethod(nameof(EntityFrameworkQueryableExtensions.ExecuteUpdateAsync))!;

    public static Expression<Func<TTo, bool>> ConvertFilter<TFrom, TTo>(Expression<Func<TFrom, bool>> expr)
    {
        var lambda = ConvertLambda(expr, typeof(TFrom), typeof(TTo), []);
        return Expression.Lambda<Func<TTo, bool>>(lambda.Body, lambda.Parameters);
    }

    public static LambdaExpression ConvertLambda(LambdaExpression expr, Type tFrom, Type tTo, Dictionary<ParameterExpression, ParameterExpression> parameter)
    {
        foreach (var p in expr.Parameters)
        {
            parameter.Add(p, p.Type == tFrom ? Expression.Parameter(tTo, p.Name) : p);
        }
        return Expression.Lambda(
            ConvertNode(expr.Body, tFrom, tTo, parameter),
            expr.Parameters.Select(p => parameter[p])
        );
    }

    [return: NotNullIfNotNull(nameof(node))]
    public static Expression? ConvertNode(Expression? node, Type tFrom, Type tTo, Dictionary<ParameterExpression, ParameterExpression> parameter) => node switch
    {
        null => null,
        ConstantExpression ce => ce,
        MemberExpression me when me.Member.DeclaringType == tFrom
            => Expression.PropertyOrField(ConvertNode(me.Expression, tFrom, tTo, parameter)!, me.Member.Name),
        MemberExpression me => me,
        BinaryExpression be => Expression.MakeBinary(be.NodeType, ConvertNode(be.Left, tFrom, tTo, parameter), ConvertNode(be.Right, tFrom, tTo, parameter), be.IsLiftedToNull, be.Method),
        UnaryExpression ue => Expression.MakeUnary(ue.NodeType, ConvertNode(ue.Operand, tFrom, tTo, parameter), ue.Type, ue.Method),
        LambdaExpression le => ConvertLambda(le, tFrom, tTo, parameter),
        ParameterExpression pe => parameter[pe],
        MethodCallExpression mce => Expression.Call(mce.Object, mce.Method, mce.Arguments.Select(a => ConvertNode(a, tFrom, tTo, parameter))),
        _ => throw new NotSupportedException(node.NodeType.ToString()),
    };

    public static Task<int> ExecuteUpdateAsync<T>(this IQueryable<T> query, IReadOnlyDictionary<string, object?> fieldValues, CancellationToken cancellationToken = default)
    {
        var updateBody = BuildUpdateBody(typeof(T), fieldValues);

        return (Task<int>)UpdateAsyncMethodInfo.MakeGenericMethod(typeof(T)).Invoke(null, [query, updateBody, cancellationToken])!;
    }

    static LambdaExpression BuildUpdateBody(Type entityType, IReadOnlyDictionary<string, object?> fieldValues)
    {
        var setParam = Expression.Parameter(typeof(SetPropertyCalls<>).MakeGenericType(entityType), "s");
        var objParam = Expression.Parameter(entityType, "e");

        Expression setBody = setParam;

        foreach (var pair in fieldValues)
        {
            var propExpression = Expression.PropertyOrField(objParam, pair.Key);
            var valueExpression = ValueForType(propExpression.Type, pair.Value);

            // s.SetProperty(e => e.SomeField, value)
            setBody = Expression.Call(setBody, nameof(SetPropertyCalls<object>.SetProperty),
                [propExpression.Type], Expression.Lambda(propExpression, objParam), valueExpression);
        }

        // s => s.SetProperty(e => e.SomeField, value)
        var updateBody = Expression.Lambda(setBody, setParam);

        return updateBody;
    }

    public static Task<int> ExecuteUpdateAsync<T>(this IQueryable<T> query, IEnumerable<(Type PropType, Expression Access, Expression Set)> fieldValues, CancellationToken cancellationToken = default)
    {
        var updateBody = BuildUpdateBody(typeof(T), fieldValues);

        return (Task<int>)UpdateAsyncMethodInfo.MakeGenericMethod(typeof(T)).Invoke(null, [query, updateBody, cancellationToken])!;
    }

    static LambdaExpression BuildUpdateBody(Type entityType, IEnumerable<(Type PropType, Expression Access, Expression Set)> fieldValues)
    {
        var setParam = Expression.Parameter(typeof(SetPropertyCalls<>).MakeGenericType(entityType), "s");
        var objParam = Expression.Parameter(entityType, "e");

        Expression setBody = setParam;

        foreach (var pair in fieldValues)
        {
            var expressionSet = pair.PropType.IsGenericType && pair.PropType.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? Expression.Convert(pair.Set, pair.PropType)
                : pair.Set;

            // s.SetProperty(e => e.SomeField, value)
            setBody = Expression.Call(setBody, nameof(SetPropertyCalls<object>.SetProperty),
                    [pair.PropType], pair.Access, expressionSet);
        }

        // s => s.SetProperty(e => e.SomeField, value)
        var updateBody = Expression.Lambda(setBody, setParam);

        return updateBody;
    }

    static Expression ValueForType(Type desiredType, object? value)
    {
        if (value == null)
        {
            return Expression.Default(desiredType);
        }

        if (value.GetType() != desiredType)
        {
            return Expression.Convert(Expression.Constant(value), desiredType);
        }

        return Expression.Constant(value);
    }
}
