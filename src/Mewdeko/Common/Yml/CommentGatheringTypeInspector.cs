﻿using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;

namespace Mewdeko.Common.Yml;

/// <summary>
///     Type inspector that gathers comments associated with properties during YAML serialization.
/// </summary>
public class CommentGatheringTypeInspector : TypeInspectorSkeleton
{
    private readonly ITypeInspector innerTypeDescriptor;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommentGatheringTypeInspector" /> class.
    /// </summary>
    /// <param name="innerTypeDescriptor">The inner type inspector to decorate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerTypeDescriptor" /> is null.</exception>
    public CommentGatheringTypeInspector(ITypeInspector innerTypeDescriptor)
    {
        this.innerTypeDescriptor =
            innerTypeDescriptor ?? throw new ArgumentNullException(nameof(innerTypeDescriptor));
    }

    /// <inheritdoc />
    public override string GetEnumName(Type enumType, string name)
    {
        return name;
    }

    /// <inheritdoc />
    public override string? GetEnumValue(object enumValue)
    {
        return enumValue.ToString();
    }

    /// <inheritdoc />
    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
    {
        return innerTypeDescriptor
            .GetProperties(type, container)
            .Select(d => new CommentsPropertyDescriptor(d));
    }

    private sealed class CommentsPropertyDescriptor(IPropertyDescriptor baseDescriptor) : IPropertyDescriptor
    {
        /// <inheritdoc />
        public string Name { get; } = baseDescriptor.Name;

        public bool AllowNulls { get; }

        /// <inheritdoc />
        public Type Type
        {
            get
            {
                return baseDescriptor.Type;
            }
        }

        /// <inheritdoc />
        public Type? TypeOverride
        {
            get
            {
                return baseDescriptor.TypeOverride;
            }
            set
            {
                baseDescriptor.TypeOverride = value;
            }
        }

        /// <inheritdoc />
        public int Order { get; set; }

        /// <inheritdoc />
        public ScalarStyle ScalarStyle
        {
            get
            {
                return baseDescriptor.ScalarStyle;
            }
            set
            {
                baseDescriptor.ScalarStyle = value;
            }
        }

        public bool Required { get; }
        public Type? ConverterType { get; }

        /// <inheritdoc />
        public bool CanWrite
        {
            get
            {
                return baseDescriptor.CanWrite;
            }
        }

        /// <inheritdoc />
        public void Write(object target, object? value)
        {
            baseDescriptor.Write(target, value);
        }

        /// <inheritdoc />
        public T? GetCustomAttribute<T>() where T : Attribute
        {
            return baseDescriptor.GetCustomAttribute<T>();
        }

        /// <inheritdoc />
        public IObjectDescriptor Read(object target)
        {
            var comment = baseDescriptor.GetCustomAttribute<CommentAttribute>();
            return comment is not null
                ? new CommentsObjectDescriptor(baseDescriptor.Read(target), comment.Comment)
                : baseDescriptor.Read(target);
        }
    }
}