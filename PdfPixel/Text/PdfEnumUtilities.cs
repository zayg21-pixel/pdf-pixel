using PdfPixel.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif

namespace PdfPixel.Text
{
    /// <summary>
    /// Provides utilities for mapping between PDF string values and enum values decorated with <see cref="PdfEnumAttribute"/>.
    /// </summary>
    internal static class PdfEnumUtilities
    {
        /// <summary>
        /// Caches enum type to mapping of <see cref="PdfString"/> to enum value.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<PdfString, Enum>> EnumValueCache =
[];

        /// <summary>
        /// Caches enum type to mapping of enum value to <see cref="PdfString"/>.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<Enum, PdfString>> EnumInverseValueCache =
[];

        /// <summary>
        /// Converts a <see cref="PdfString"/> to its corresponding enum value of type <typeparamref name="T"/>.
        /// Uses <see cref="PdfEnumValueAttribute"/> if present, otherwise uses enum name.
        /// Returns the default value if the <see cref="PdfString"/> is empty or not found.
        /// Results are cached for performance.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="value">PDF string value to convert</param>
        /// <returns>Enum value of type <typeparamref name="T"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsEnum<
#if !NETSTANDARD2_0
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        T
            >(this in PdfString value) where T : Enum
        {
            Type enumType = typeof(T);
            Dictionary<PdfString, Enum> map = EnumValueCache.GetOrAdd(enumType, _ => BuildEnumValueMap<T>());

            if (value.IsEmpty)
            {
                return default;
            }

            if (map.TryGetValue(value, out Enum enumValue))
            {
                return (T)enumValue;
            }

            // Return default value if not found
            return default;
        }

        /// <summary>
        /// Converts an enum value of type <typeparamref name="T"/> to its corresponding <see cref="PdfString"/> using the <see cref="PdfEnumValueAttribute"/>.
        /// Returns the default value's <see cref="PdfString"/> if the enum value is default(T).
        /// </summary>
        /// <typeparam name="T">Enum type decorated with <see cref="PdfEnumAttribute"/></typeparam>
        /// <param name="enumValue">Enum value to convert</param>
        /// <returns><see cref="PdfString"/> representation of the enum value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PdfString AsPdfString<
#if !NETSTANDARD2_0
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
#endif
            T
            >(this T enumValue) where T : Enum
        {
            Type enumType = typeof(T);
            Dictionary<Enum, PdfString> inverseMap = EnumInverseValueCache.GetOrAdd(enumType, _ => BuildEnumInverseValueMap<T>());

            if (inverseMap.TryGetValue(enumValue, out PdfString pdfString))
            {
                return pdfString;
            }

            // Fallback: empty PdfString
            return default;
        }

        /// <summary>
        /// Builds a mapping from <see cref="PdfString"/> to enum value for the given enum type.
        /// Validates presence of <see cref="PdfEnumAttribute"/> and <see cref="PdfEnumValueAttribute"/> attributes.
        /// Also builds the inverse map from enum value to <see cref="PdfString"/>.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>Dictionary mapping <see cref="PdfString"/> to enum value</returns>
        private static Dictionary<PdfString, Enum> BuildEnumValueMap<
#if !NETSTANDARD2_0
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        T>() where T : Enum
        {
            Type enumType = typeof(T);
            if (enumType.GetCustomAttributes(typeof(PdfEnumAttribute), inherit: false).Length == 0)
            {
                throw new ArgumentException($"Enum type '{enumType.FullName}' must be decorated with [PdfEnum] attribute.", nameof(T));
            }

            Dictionary<PdfString, Enum> map = [];
            FieldInfo defaultField = null;
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                PdfEnumValueAttribute valueAttr = field.GetCustomAttribute<PdfEnumValueAttribute>();
                PdfEnumDefaultValueAttribute defaultAttr = field.GetCustomAttribute<PdfEnumDefaultValueAttribute>();
                if (valueAttr == null && defaultAttr == null)
                {
                    throw new ArgumentException($"Enum field '{field.Name}' in '{enumType.FullName}' must be decorated with either [PdfEnumValue] or [PdfEnumDefaultValue] attribute.", nameof(T));
                }

                if (defaultAttr != null)
                {
                    defaultField = field;
                }

                string name = valueAttr?.Name ?? string.Empty;
                if (defaultAttr != null)
                {
                    name = valueAttr?.Name ?? string.Empty;
                }

                PdfString pdfString = new(EncodingExtensions.PdfDefault.GetBytes(name));
                var enumValue = (Enum)field.GetValue(null);
                map[pdfString] = enumValue;
            }

            if (defaultField == null)
            {
                throw new ArgumentException($"Enum type '{enumType.FullName}' must have one field decorated with [PdfEnumDefaultValue] attribute.", nameof(T));
            }

            var defaultValue = (Enum)defaultField.GetValue(null);
            if (!defaultValue.Equals(default(T)))
            {
                throw new ArgumentException($"Enum type '{enumType.FullName}': the field marked with [PdfEnumDefaultValue] must be equal to default({enumType.Name}).", nameof(T));
            }

            return map;
        }

        /// <summary>
        /// Builds a mapping from enum value to <see cref="PdfString"/> for the given enum type using the forward map.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>Dictionary mapping enum value to <see cref="PdfString"/></returns>
        private static Dictionary<Enum, PdfString> BuildEnumInverseValueMap<
#if !NETSTANDARD2_0
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        T
            >() where T : Enum
        {
            Type enumType = typeof(T);
            Dictionary<PdfString, Enum> forwardMap = EnumValueCache.GetOrAdd(enumType, _ => BuildEnumValueMap<T>());
            Dictionary<Enum, PdfString> inverseMap = [];
            foreach (KeyValuePair<PdfString, Enum> kvp in forwardMap)
            {
                inverseMap[kvp.Value] = kvp.Key;
            }

            return inverseMap;
        }
    }
}
