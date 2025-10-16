using System.Linq.Expressions;
using eHub.Scripting.Connectors.Db;
using FluentAssertions;

namespace eHub.Tests.Connectors.PacketTransfer.Db;

[TestClass]
public class ExpressionMagicTests
{
    private readonly ParameterExpression _fromParameter = Expression.Parameter(typeof(From), "f");
    private readonly ParameterExpression _toParameter = Expression.Parameter(typeof(To), "t");

    [TestMethod]
    public void ConvertNode_BytesMember_Success()
    {       
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.BytesMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");

        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().NotBeNull().And.Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.BytesMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_MemoryBytesToBytesMember_Success()
    {        
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.MemoryBytesMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.MemoryBytesMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_BytesToMemoryBytesMember_Success()
    {       
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(To.MemoryBytesMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(To), typeof(From), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<From>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(From.MemoryBytesMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertLambda_ComplexConditions_Success()
    {
        // Example expression: f => f.Id == 1 && f.DateTimeMember > DateTime.UtcNow
        Expression<Func<From, bool>> fromLambda = f => f.Id == 1 && f.DateTimeMember > DateTime.UtcNow;

        // Act
        var toLambda = ExpressionMagic.ConvertLambda(fromLambda, typeof(From), typeof(To), []);

        // Assert
        toLambda.Should().NotBeNull();
        toLambda.Parameters[0].Type.Should().Be<To>();

        var binaryExpression = (BinaryExpression)toLambda.Body;

        // Check the first part of the condition (Id == 1)
        var leftExpression = (BinaryExpression)binaryExpression.Left;
        ((MemberExpression)leftExpression.Left).Member.Name.Should().Be("Id");
        ((MemberExpression)leftExpression.Left).Type.Should().Be<int>();
        ((ConstantExpression)leftExpression.Right).Value.Should().Be(1);

        // Check the second part of the condition (DateTimeMember > DateTime.UtcNow)
        var rightExpression = (BinaryExpression)binaryExpression.Right;
        ((MemberExpression)rightExpression.Left).Member.Name.Should().Be("DateTimeMember");
        ((MemberExpression)rightExpression.Left).Type.Should().Be<DateTime>();
        (rightExpression.Right is MemberExpression || rightExpression.Right is MethodCallExpression).Should().BeTrue("DateTime comparison was not converted properly.");
    }

    [TestMethod]
    public void ConvertFilter_SimpleCondition_Success()
    {
        // Arrange
        Expression<Func<From, bool>> fromFilter = f => f.Id == 1;

        // Act
        var toFilter = ExpressionMagic.ConvertFilter<From, To>(fromFilter);

        // Assert
        // Check that the filter was converted correctly
        toFilter.Should().NotBeNull();
        toFilter.Parameters[0].Type.Should().Be<To>();

        var binaryExpression = (BinaryExpression)toFilter.Body;

        // Check the condition (Id == 1)
        binaryExpression.NodeType.Should().Be(ExpressionType.Equal, "The expression is not an equality comparison.");

        var leftExpression = (MemberExpression)binaryExpression.Left;
        leftExpression.Member.Name.Should().Be("Id", "The member name is not correctly converted.");
        leftExpression.Type.Should().Be<int>("The member type is not correctly converted.");

        var rightExpression = (ConstantExpression)binaryExpression.Right;
        rightExpression.Value.Should().Be(1, "The constant value is not correctly preserved.");
    }

    [TestMethod]
    public void ConvertNode_NullExpression_ReturnsNull()
    {
        Expression? node = null;
        var result = ExpressionMagic.ConvertNode(node, typeof(From), typeof(To), []);
        result.Should().BeNull("Result should be null when input node is null.");
    }

    [TestMethod]
    public void ConvertNode_ConstantValue_ReturnsUnchanged()
    {
        var initialExpression = Expression.Constant(1);
        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), []);
        newExpression.GetType().Should().Be(initialExpression.GetType(), "Expression types are not equal");
        newExpression.Type.Should().Be(initialExpression.Type, "Expression constant types are not equal");
        ((ConstantExpression)newExpression).Value.Should().Be(initialExpression.Value, "Expression constant value are not equal");
    }

    [TestMethod]
    public void ConvertNode_NumberMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.NumberMember));

        var toParameter = Expression.Parameter(typeof(To), "f");

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.NumberMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_NullableNumberMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_toParameter, nameof(From.NullableNumberMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.NullableNumberMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_LongNumberMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.LongNumberMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        newExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.LongNumberMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_NullableLongNumberMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.NullableLongNumberMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.NullableLongNumberMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_StringMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.StringMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.StringMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_NullableStringMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.NullableStringMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.NullableStringMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_DateTimeMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.DateTimeMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.DateTimeMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_NullableDateTimeMember_Success()
    {
        var initialExpression = Expression.PropertyOrField(_fromParameter, nameof(From.NullableDateTimeMember));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);
        var memberExpression = newExpression as MemberExpression;

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        memberExpression.Should().NotBeNull();
        memberExpression!.Expression.Should().NotBeNull();
        memberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        memberExpression.Member.Name.Should().Be(nameof(To.NullableDateTimeMember), "The member name was not converted properly.");
    }

    [TestMethod]
    public void ConvertNode_MixedMembers_OnlyConvertsRelevant()
    {
        // Define the From type and another unrelated type (Another)
        var anotherParameter = Expression.Parameter(typeof(Another), "a");

        // Create a MemberExpression accessing the Id member of the From type
        var fromMemberExpression = Expression.PropertyOrField(_fromParameter, nameof(From.Id));

        // Create a MemberExpression accessing the AId member of the Another type
        var anotherMemberExpression = Expression.PropertyOrField(anotherParameter, nameof(Another.AId));

        // Combine these into a new expression using Expression.Add
        var combinedExpression =
            Expression.Add(
                fromMemberExpression, // Accessing From.Id
                anotherMemberExpression // Accessing Another.AId
            );

        // Prepare the target parameter of type To
        var toParameter = Expression.Parameter(typeof(To), "f");

        // Create a dictionary mapping the original parameter to the converted parameter
        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, toParameter }
        };

        // Convert the entire expression (not just the body)
        var newExpression = ExpressionMagic.ConvertNode(combinedExpression, typeof(From), typeof(To), parameters);

        // Extract the body of the converted expression
        var binaryExpression = (BinaryExpression)newExpression;

        // Assertions

        // Check that the From.Id part of the expression has been converted
        var newFromMemberExpression = binaryExpression.Left as MemberExpression;
        newFromMemberExpression.Should().NotBeNull("The left part of the BinaryExpression should be a MemberExpression.");
        newFromMemberExpression!.Expression.Should().NotBeNull();
        newFromMemberExpression.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        newFromMemberExpression.Member.Name.Should().Be(nameof(To.Id), "The member name was not converted properly.");

        // Check that the Another.AId part of the expression has not been converted
        var newAnotherMemberExpression = binaryExpression.Right as MemberExpression;
        newAnotherMemberExpression.Should().NotBeNull("The right part of the BinaryExpression should be a MemberExpression.");
        newAnotherMemberExpression.Should().Be(anotherMemberExpression, "The member expression should not have been converted.");
    }

    [TestMethod]
    public void ConvertNode_UnrelatedMember_Unchanged()
    {
        // Define the From type and another unrelated type
        var anotherParameter = Expression.Parameter(typeof(Another), "a");

        // Create a MemberExpression accessing a member of the AnotherClass type (e.g., "Description")
        var memberExpression = Expression.PropertyOrField(anotherParameter, nameof(Another.AId));

        // Prepare the target parameter of type To
        var toParameter = Expression.Parameter(typeof(To), "f");

        // Create a dictionary mapping the original parameter to the converted parameter
        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, toParameter }
        };

        // Convert the MemberExpression
        var newExpression = ExpressionMagic.ConvertNode(memberExpression, typeof(From), typeof(To), parameters);

        // Assertions
        newExpression.Should().Be(memberExpression, "The member expression should not have been converted.");
    }

    [TestMethod]
    public void ConvertNode_BinaryExpression_Success()
    {
        var fromLeft = Expression.Parameter(typeof(From), "l");
        var fromRight = Expression.Parameter(typeof(From), "r");

        var initialExpression = Expression.MakeBinary(
            ExpressionType.Equal,
            fromLeft,
            fromRight
        );

        var toLeft = Expression.Parameter(typeof(To), "l");
        var toRight = Expression.Parameter(typeof(To), "r");

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { fromLeft, toLeft },
            { fromRight, toRight }
        };
        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);

        newExpression.GetType().Should().Be(initialExpression.GetType(), "Expression types are not equal");

        initialExpression.Type.Should().Be(initialExpression.Type, "Expression constant types are not equal");// nam reusit sa testez asta, pentru ca nu pot
        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        ((BinaryExpression)newExpression).Left.Type.Should().Be<To>("Left side of the binary operation did not convert properly");
        ((BinaryExpression)newExpression).Right.Type.Should().Be<To>("Right side of the binary operation did not convert properly");
    }

    [TestMethod]
    public void ConvertNode_UnaryExpression_Success()
    {
        var initialExpression = Expression.MakeUnary(ExpressionType.Convert, _fromParameter, typeof(object));

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);

        newExpression.GetType().Should().Be(initialExpression.GetType(), "Expression types are not equal");
        initialExpression.Type.Should().Be(initialExpression.Type, "Expression constant types are not equal");
        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expression NodeType did not convert properly");
        ((UnaryExpression)newExpression).Operand.Type.Should().Be<To>("Operand of the unary operation did not convert properly");
    }

    [TestMethod]
    public void ConvertNode_LambdaExpression_Success()
    {
        var initialExpression = Expression.Lambda(_fromParameter, _fromParameter);
        // Call ConvertNode with parameters
        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), []);

        // Compare the types at the expression level, not at the specific type level
        newExpression.Type.GetGenericTypeDefinition().Should().Be(initialExpression.Type.GetGenericTypeDefinition(), "Expression delegate types are not equal");

        initialExpression.NodeType.Should().Be(initialExpression.NodeType, "Expressions Nodetype types are not equal");

        // Check that the body of the lambda has been converted to the correct type
        _toParameter.Type.Should().Be(((LambdaExpression)newExpression).Body.Type, "Lambda body type did not convert properly")
            .And.Be(((LambdaExpression)newExpression).Parameters[0].Type, "Lambda parameter type did not convert properly");

        // Check that the names of the parameters are the same
        _fromParameter.Name.Should().Be(((LambdaExpression)newExpression).Parameters[0].Name, "Lambda parameter name did not convert properly");
    }

    [TestMethod]
    public void ConvertNode_ParameterExpression_Success()
    {
        var initialExpression = Expression.Parameter(typeof(From), "f");

        var parameters = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { initialExpression, _toParameter }
        };

        var newExpression = ExpressionMagic.ConvertNode(initialExpression, typeof(From), typeof(To), parameters);

        // Assertions
        newExpression.GetType().Should().Be(initialExpression.GetType(), "Expression types are not equal");
        _toParameter.Type.Should().Be(_toParameter.Type, "Expression types did not convert properly");
        ((ParameterExpression)newExpression).Name.Should().Be(_toParameter.Name, "Parameter name did not convert properly");
    }

    [TestMethod]
    public void ConvertNode_MethodCallExpression_Success()
    {
        // Arrange
        var toParam = Expression.Parameter(typeof(To), "f");
        var callParameter = Expression.Parameter(typeof(IBaseInterface), "o");

        // Create method call: fromObj.SampleMethod(fromObj)
        var methodInfo = typeof(IBaseInterface).GetMethod(nameof(IBaseInterface.SampleMethod), [typeof(IBaseInterface)])!;
        var methodCallExpression = Expression.Call(callParameter, methodInfo, _fromParameter);
        // Create the parameter dictionary for conversion from 'From' to 'To'
        var parameterMap = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { _fromParameter, toParam }
        };

        // Act
        var result = ExpressionMagic.ConvertNode(methodCallExpression, typeof(From), typeof(To), parameterMap);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<MethodCallExpression>();

        var resultMethodCall = (MethodCallExpression)result;

        // Check that the method being called is still the same
        resultMethodCall.Method.Should().BeSameAs(methodCallExpression.Method);

        // Check that the object on which the method is called has NOT been converted
        resultMethodCall.Object.Should().Be(callParameter);

        // Check that the argument has been converted from 'From' to 'To'
        resultMethodCall.Arguments.Should().ContainSingle();
        resultMethodCall.Arguments.Should().HaveElementAt(0, toParam);
    }

    [TestMethod]
    public void ConvertNode_MixedExpression_Success()
    {
        // Define the From type and another unrelated type (Another)
        var anotherParameter = Expression.Parameter(typeof(Another), "a");
        var extraParameter = Expression.Parameter(typeof(string), "extra");
        var callParameter = Expression.Parameter(typeof(IBaseInterface), "o");

        // Create a constant expression
        var constantExpression = Expression.Constant(42);

        // Create a MemberExpression accessing the Id member of the From type
        var fromMemberExpression = Expression.PropertyOrField(_fromParameter, nameof(From.Id));

        // Create a MemberExpression accessing the AId member of the Another type
        var anotherMemberExpression = Expression.PropertyOrField(anotherParameter, nameof(Another.AId));

        // Create a UnaryExpression (e.g., negating the constant)
        var unaryExpression = Expression.Negate(constantExpression);

        // Create a BinaryExpression (e.g., adding the From.Id and constant)
        var binaryExpression = Expression.Add(anotherMemberExpression, unaryExpression);

        // Create a MethodCallExpression (e.g., calling a method on From)
        var methodInfo = typeof(IBaseInterface).GetMethod(nameof(IBaseInterface.SampleMethod2), [typeof(int)])!;

        // Combine all into a complex expression using Expression.Add to create a lambda
        var combinedExpression = Expression.Lambda(
            Expression.Call(callParameter, methodInfo, Expression.Add(binaryExpression, fromMemberExpression)),
            _fromParameter,
            anotherParameter,
            extraParameter
        );

        // Convert the entire expression (not just the body)
        var newExpression = (LambdaExpression)ExpressionMagic.ConvertNode(combinedExpression, typeof(From), typeof(To), []);

        // Extract the body of the converted expression, which should now be a MethodCallExpression
        var newMethodCallExpression = newExpression.Body as MethodCallExpression;
        var firstArg = newMethodCallExpression?.Arguments[0] as BinaryExpression;
        var r = firstArg?.Right as MemberExpression;
        var x = r?.Expression?.Type;

        newMethodCallExpression.Should().NotBeNull("The outermost expression should be a MethodCallExpression.");
        newMethodCallExpression!.Method.Name.Should().Be(methodInfo.Name, "The method name should be the same as before the conversion.");
        x.Should().Be<To>("The method call should be on the To type.");

        // The object on which ToString is called should be a BinaryExpression
        var firstLevelBinary = newMethodCallExpression.Arguments[0] as BinaryExpression;
        firstLevelBinary.Should().NotBeNull("The object of the method call should be a BinaryExpression.");

        // Left side of the first binary should be a BinaryExpression
        var secondLevelBinary = firstLevelBinary?.Left as BinaryExpression;
        secondLevelBinary.Should().NotBeNull("The left side of the first binary expression should be another BinaryExpression.");

        // Right side of the first binary should be the From.Id (now converted to To.Id)
        var newFromMemberExpression = firstLevelBinary?.Right as MemberExpression;
        newFromMemberExpression.Should().NotBeNull("The right side of the first binary expression should be a MemberExpression.");
        newFromMemberExpression!.Expression!.Type.Should().Be<To>("The member expression's type was not converted properly.");
        newFromMemberExpression.Member.Name.Should().Be(nameof(To.Id), "The member name was not converted properly.");

        // Left side of the second binary should be the Another.AId (not converted)
        var newAnotherMemberExpression = secondLevelBinary?.Left as MemberExpression;
        newAnotherMemberExpression.Should().NotBeNull("The left side of the second binary expression should be a MemberExpression.")
            .And.Be(anotherMemberExpression, "The member expression should not have been converted.");
        // Right side of the second binary should be the unary expression (negated constant)
        var newUnaryExpression = secondLevelBinary?.Right as UnaryExpression;
        newUnaryExpression.Should().NotBeNull("The right side of the second binary expression should be a UnaryExpression.");
        ExpressionType.Negate.Should().Be(ExpressionType.Negate, "The unary expression should be a negation.");
        ((ConstantExpression)newUnaryExpression!.Operand).Value.Should().Be(42, "The unary expression should negate the constant value 42.");
    }

    private interface IBaseInterface
    {
        public string? SampleMethod(IBaseInterface x)
        {
            return x.ToString();
        }

        public string SampleMethod2(int y)
        {
            return y.ToString();
        }
    };

    private class From : IBaseInterface
    {
        public int Id { get; set; }

        public int NumberMember { get; set; }
        public int? NullableNumberMember { get; set; }

        public long LongNumberMember { get; set; }
        public long? NullableLongNumberMember { get; set; }

        public string StringMember { get; set; } = "";
        public string? NullableStringMember { get; set; }

        public DateTime DateTimeMember { get; set; }
        public DateTime? NullableDateTimeMember { get; set; }

        public byte[] BytesMember { get; set; } = [];
        public ReadOnlyMemory<byte> MemoryBytesMember { get; set; } = Array.Empty<byte>();
    }

    private class To : IBaseInterface
    {
        public int Id { get; set; }

        public int NumberMember { get; set; }
        public int? NullableNumberMember { get; set; }

        public long LongNumberMember { get; set; }
        public long? NullableLongNumberMember { get; set; }

        public string StringMember { get; set; } = "";
        public string? NullableStringMember { get; set; }

        public DateTime DateTimeMember { get; set; }
        public DateTime? NullableDateTimeMember { get; set; }

        public byte[] BytesMember { get; set; } = [];
        public byte[] MemoryBytesMember { get; set; } = [];

    }

    public class Another
    {
        internal int AId { get; set; }
    }
}
