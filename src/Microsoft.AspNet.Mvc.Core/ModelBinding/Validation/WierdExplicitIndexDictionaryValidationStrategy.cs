// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNet.Mvc.ModelBinding.Validation
{
    public class WierdExplicitIndexDictionaryValidationStrategy<TKey, TValue> : IValidationStrategy
    {
        private readonly IEnumerable<KeyValuePair<string, TKey>> _keyMappings;
        private readonly ModelMetadata _valueMetadata;

        public WierdExplicitIndexDictionaryValidationStrategy(IEnumerable<KeyValuePair<string, TKey>> keyMappings, ModelMetadata valueMetadata)
        {
            _keyMappings = keyMappings;
            _valueMetadata = valueMetadata;
        }

        public IEnumerator<ValidationEntry> GetChildren(
            ModelMetadata metadata,
            string key,
            object model)
        {
            return new Enumerator(_valueMetadata, key, _keyMappings, (IDictionary<TKey, TValue>)model);
        }

        private class Enumerator : IEnumerator<ValidationEntry>
        {
            private readonly ValidationEntry _entry;
            private readonly string _key;
            private readonly IDictionary<TKey, TValue> _model;
            private readonly IEnumerator<KeyValuePair<string, TKey>> _keyMappingEnumerator;

            public Enumerator(
                ModelMetadata metadata,
                string key,
                IEnumerable<KeyValuePair<string, TKey>> keyMappings,
                IDictionary<TKey, TValue> model)
            {
                _key = key;
                _model = model;

                _keyMappingEnumerator = keyMappings.GetEnumerator();

                _entry = new ValidationEntry()
                {
                    Metadata = metadata,
                };
            }

            public ValidationEntry Current
            {
                get
                {
                    return _entry;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public bool MoveNext()
            {
                TValue value;
                while (true)
                {
                    if (!_keyMappingEnumerator.MoveNext())
                    {
                        return false;
                    }

                    if (_model.TryGetValue(_keyMappingEnumerator.Current.Value, out value))
                    {
                        // Skip over entries that we can't find in the dictionary, they will show up as unvalidated.
                        break;
                    }
                }

                var key = ModelNames.CreateIndexModelName(_key, _keyMappingEnumerator.Current.Key);
                var model = value;

                _entry.Key = key;
                _entry.Model = model;

                return true;
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
