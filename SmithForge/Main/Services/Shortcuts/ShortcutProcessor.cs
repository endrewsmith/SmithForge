using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.Shortcuts
{
    public class ShortcutProcessor
    {
        private readonly List<IShortcutRule> _rules = new();

        public void Register(IShortcutRule rule)
        {
            if (rule != null) _rules.Add(rule);
        }

        public void RegisterRange(IEnumerable<IShortcutRule> rules)
        {
            foreach (var rule in rules) Register(rule);
        }

        /// <summary>
        /// Прогнать сообщение через все зарегистрированные правила.
        /// </summary>
        public string Process(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;

            string result = message;
            foreach (var rule in _rules)
            {
                try
                {
                    string newResult = rule.Apply(result);
                    if (!ReferenceEquals(newResult, result) && newResult != result)
                    {
                        Debug.WriteLine($"[Shortcuts] ✅ {rule.Name}: '{result}' → '{newResult}'");
                        result = newResult;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Shortcuts] ❌ {rule.Name}: {ex.Message}");
                }
            }

            return result;
        }
    }
}