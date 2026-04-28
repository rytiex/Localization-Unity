//#define NETCODE

using UnityEngine;
using Unity.VisualScripting;
using System;

namespace PicoShot.Localization
{
    /// <summary>
    /// TextNode represents a text structure composed of multiple nodes instead of a plain string.
    /// 
    /// A node can contain:
    /// - Plain text
    /// - Localized text entries
    /// - Formatting information (color, bold, etc.)
    /// - Other nested TextNodes as arguments
    /// 
    /// This allows complex text structures to be serialized and transferred over the network
    /// without losing localization or formatting data.
    /// 
    /// Example:
    /// A message sent from one player to another can be reconstructed and localized
    /// on the receiving side using that player's selected language.
    /// 
    /// Main use case:
    /// Multiplayer/networked games where text should automatically appear
    /// in the recipient's own language.
    /// 
    /// NOTE:
    /// If your project uses Netcode, add "NETCODE" to your
    /// Scripting Define Symbols.
    /// </summary>
    public struct TextNode 
#if NETCODE
        : INetworkSerializable
#endif
    {
        public string Value;
        public NodeType Type;
        public TextNode[] Nodes;
        public RichModifier[] RichModifiers;

        public TextNode(string value)
        {
            Value = value;
            Type = NodeType.PlainText;
            Nodes = Array.Empty<TextNode>();
            RichModifiers = Array.Empty<RichModifier>();
        }

        public static TextNode Text(string text, params TextNode[] arguments)
        {
            return new TextNode()
            {
                Value = text,
                Type = NodeType.PlainText,
                Nodes = arguments
            };
        }
        public static TextNode Localized(string translationKeyName, params TextNode[] arguments)
        {
            return new TextNode()
            {
                Value = translationKeyName,
                Type = NodeType.LocalizedText,
                Nodes = arguments
            };
        }
        public static TextNode Formatted(string format, params TextNode[] arguments)
        {
            return new TextNode()
            {
                Value = format,
                Type = NodeType.FormattedText,
                Nodes = arguments
            };
        }

        private string ApplyModifiers(in string text)
        {
            if (RichModifiers == null || RichModifiers.Length == 0)
                return text;

            string output = string.Empty;

            for (int i = 0; i < RichModifiers.Length; i++)
                output += RichModifiers[i].Begin();

            output += text;

            for (int i = RichModifiers.Length - 1; i >= 0; i--)
                output += RichModifiers[i].End();

            return output;
        }

        public override string ToString()
        {
            if (Type == NodeType.PlainText)
                return ApplyModifiers(Value);

            var arguments = new string[Nodes.Length];
            for (int i = 0; i < Nodes.Length; i++)
                arguments[i] = Nodes[i].ToString();

            return ApplyModifiers(Type switch
            {
                NodeType.LocalizedText => LocalizationManager.GetText(Value, arguments),
                NodeType.FormattedText => string.Format(Value, arguments),
                _ => throw new NotImplementedException()
            });
        }

        public TextNode AddModifier(RichModifier modifier)
        {
            if (RichModifiers != null)
                Array.Resize(ref RichModifiers, RichModifiers.Length + 1);
            else
                RichModifiers = new RichModifier[1];

            RichModifiers[^1] = modifier;

            return this;
        }

#if NETCODE
        void INetworkSerializable.NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref Value);
            serializer.SerializeValue(ref Type);

            int nodeCount = Nodes != null ? Nodes.Length : 0;
            serializer.SerializeValue(ref nodeCount);

            if (serializer.IsReader)
                Nodes = new TextNode[nodeCount];

            if (nodeCount > 0)
                for (int i = 0; i < nodeCount; i++)
                    serializer.SerializeNetworkSerializable(ref Nodes[i]);

            int modifiers = RichModifiers != null ? RichModifiers.Length : 0;
            serializer.SerializeValue(ref modifiers);

            if (serializer.IsReader)
                RichModifiers = new RichModifier[modifiers];

            if (modifiers > 0)
                for (int i = 0; i < modifiers; i++)
                    serializer.SerializeNetworkSerializable(ref RichModifiers[i]);
        }
#endif

        public static implicit operator string(TextNode node) => node.ToString();
        public static implicit operator TextNode(string value) => new(value);

        public enum NodeType : byte
        {
            PlainText = 0,
            LocalizedText = 1,
            FormattedText = 2
        }
    }

    public struct RichModifier 
#if NETCODE
    : INetworkSerializable
#endif
    {
        public string Tag;
        public string Parameter;

        public RichModifier(string tag, string parameter = null)
        {
            Tag = tag;
            Parameter = parameter ?? string.Empty;
        }

        public string Begin()
        {
            return $"<{Tag}{(!string.IsNullOrEmpty(Parameter) ? $"={Parameter}" : string.Empty)}>";
        }

        public string End()
        {
            return $"</{Tag}>";
        }

        public string Apply(string text)
        {
            return $"{Begin()}{text}{End()}";
        }
#if NETCODE
        void INetworkSerializable.NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            Tag ??= string.Empty;
            serializer.SerializeValue(ref Tag);

            Parameter ??= string.Empty;
            serializer.SerializeValue(ref Parameter);
        }
#endif
    }

    public static class RichExtenstions
    {
        static string ToHexString(this Color color)
        {
            return ((byte)(color.r * 255f)).ToString("X2") + ((byte)(color.g * 255f)).ToString("X2") + ((byte)(color.b * 255f)).ToString("X2") + ((byte)(color.a * 255f)).ToString("X2");
        }

        public static TextNode BoldModifier(this TextNode node)
        {
            return node.AddModifier(new RichModifier("b"));
        }

        public static TextNode ItalicModifier(this TextNode node)
        {
            return node.AddModifier(new RichModifier("i"));
        }

        public static TextNode ColorModifier(this TextNode node, Color color)
        {
            return node.AddModifier(new RichModifier("color", $"#{color.ToHexString().ToLower()}"));
        }

        public static TextNode ColorModifier(this TextNode node, string color)
        {
            return node.AddModifier(new RichModifier("color", color));
        }

        public static TextNode SizeModifier(this TextNode node, string size)
        {
            return node.AddModifier(new RichModifier("size", size));
        }

        public static TextNode StyleModifier(this TextNode node, string style)
        {
            return node.AddModifier(new RichModifier("style", style));
        }



        public static TextNode BoldModifier(this string node)
        {
            return new TextNode(node).AddModifier(new RichModifier("b"));
        }

        public static TextNode ItalicModifier(this string node)
        {
            return new TextNode(node).AddModifier(new RichModifier("i"));
        }

        public static TextNode ColorModifier(this string node, Color color)
        {
            return new TextNode(node).AddModifier(new RichModifier("color", $"#{color.ToHexString().ToLower()}"));
        }

        public static TextNode ColorModifier(this string node, string color)
        {
            return new TextNode(node).AddModifier(new RichModifier("color", color));
        }

        public static TextNode SizeModifier(this string node, string size)
        {
            return new TextNode(node).AddModifier(new RichModifier("size", size));
        }

        public static TextNode StyleModifier(this string node, string style)
        {
            return new TextNode(node).AddModifier(new RichModifier("style", style));
        }
    }
}