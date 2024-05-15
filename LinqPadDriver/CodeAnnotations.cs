using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

// ReSharper disable once CheckNamespace
namespace JetBrains.Annotations;

// Code annotations excerpted from ReSharper's default implementation.
// Here is only a small subset of those likely to be used.

[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class PublicAPIAttribute : Attribute { }

[AttributeUsage(AttributeTargets.All)]
public sealed class UsedImplicitlyAttribute : Attribute
{
	public UsedImplicitlyAttribute()
		: this(ImplicitUseKindFlags.Default) { }

	public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
		: this(ImplicitUseKindFlags.Default, targetFlags) { }

	public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags,
		ImplicitUseTargetFlags targetFlags = ImplicitUseTargetFlags.Default)
	{
		UseKindFlags = useKindFlags;
		TargetFlags = targetFlags;
	}

	public ImplicitUseKindFlags UseKindFlags { get; }

	public ImplicitUseTargetFlags TargetFlags { get; }
}

/// <summary>
/// Can be applied to attributes, type parameters, and parameters of a type assignable from <see cref="::System.Type"/> .
/// When applied to an attribute, the decorated attribute behaves the same as <see cref="UsedImplicitlyAttribute"/>.
/// When applied to a type parameter or to a parameter of type <see cref="::System.Type"/>,  indicates that the corresponding type
/// is used implicitly.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.GenericParameter |
                AttributeTargets.Parameter)]
public sealed class MeansImplicitUseAttribute : Attribute
{
	public MeansImplicitUseAttribute()
		: this(ImplicitUseKindFlags.Default) { }

	public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags)
		: this(ImplicitUseKindFlags.Default, targetFlags) { }

	public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags,
		ImplicitUseTargetFlags targetFlags = ImplicitUseTargetFlags.Default)
	{
		UseKindFlags = useKindFlags;
		TargetFlags = targetFlags;
	}

	[UsedImplicitly]
	public ImplicitUseKindFlags UseKindFlags { get; }

	[UsedImplicitly]
	public ImplicitUseTargetFlags TargetFlags { get; }
}

/// <summary>
/// Specify the details of implicitly used symbol when it is marked
/// with <see cref="MeansImplicitUseAttribute"/> or <see cref="UsedImplicitlyAttribute"/>.
/// </summary>
[Flags]
public enum ImplicitUseKindFlags
{
	Default = Access | Assign | InstantiatedWithFixedConstructorSignature,

	/// <summary>Only entity marked with attribute considered used.</summary>
	Access = 1,

	/// <summary>Indicates implicit assignment to a member.</summary>
	Assign = 2,

	/// <summary>
	/// Indicates implicit instantiation of a type with fixed constructor signature.
	/// That means any unused constructor parameters won't be reported as such.
	/// </summary>
	InstantiatedWithFixedConstructorSignature = 4,

	/// <summary>Indicates implicit instantiation of a type.</summary>
	InstantiatedNoFixedConstructorSignature = 8,
}

/// <summary>
/// Specify what is considered to be used implicitly when marked
/// with <see cref="MeansImplicitUseAttribute"/> or <see cref="UsedImplicitlyAttribute"/>.
/// </summary>
[Flags]
public enum ImplicitUseTargetFlags
{
	Default = Itself,
	Itself = 1,

	/// <summary>Members of entity marked with attribute are considered used.</summary>
	Members = 2,

	/// <summary> Inherited entities are considered used. </summary>
	WithInheritors = 4,

	/// <summary>Entity marked with attribute and all its members considered used.</summary>
	WithMembers = Itself | Members
}
