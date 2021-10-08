using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quasar.Common.Messages
{
    public static class TypeRegistry
    {
        /// <summary>
        /// 信息类型的内部索引。
        /// </summary>
        private static int _typeIndex;

        /// <summary>
        /// 向序列化器添加一个类型，以便消息可以被正确地序列化。
        /// </summary>
        /// <param name="parent">父类型，即：IMessage</param>
        /// <param name="type">要添加的类型</param>
        public static void AddTypeToSerializer(Type parent, Type type)
        {
            if (type == null || parent == null)
                throw new ArgumentNullException();

            bool isAlreadyAdded = RuntimeTypeModel.Default[parent].GetSubtypes().Any(subType => subType.DerivedType.Type == type);

            if (!isAlreadyAdded)
                RuntimeTypeModel.Default[parent].AddSubType(++_typeIndex, type);
        }

        /// <summary>
        /// 将Types添加到序列化器中。
        /// </summary>
        /// <param name="parent">父类型，即：IMessage</param>
        /// <param name="types">要添加的类型。</param>
        public static void AddTypesToSerializer(Type parent, params Type[] types)
        {
            foreach (Type type in types)
                AddTypeToSerializer(parent, type);
        }

        public static IEnumerable<Type> GetPacketTypes(Type type)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsInterface);
        }
    }
}
