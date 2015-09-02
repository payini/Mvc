// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNet.Mvc.ModelBinding.Validation
{
    public class ExplicitIndexCollectionValidationStrategy : IValidationStrategy
    {
        private readonly IEnumerable<string> _elementKeys;

        public ExplicitIndexCollectionValidationStrategy(IEnumerable<string> elementKeys)
        {
            _elementKeys = elementKeys;
        }

        public IEnumerator<ValidationEntry> GetChildren(
            ModelMetadata metadata,
            string key,
            object model)
        {
            return new Enumerator(metadata.ElementMetadata, key, _elementKeys, (IEnumerable)model);
        }

        private class Enumerator : IEnumerator<ValidationEntry>
        {
            private readonly ValidationEntry _entry;
            private readonly string _key;
            private readonly IEnumerator _enumerator;
            private readonly IEnumerator<string> _keyEnumerator;

            public Enumerator(
                ModelMetadata metadata,
                string key,
                IEnumerable<string> elementKeys,
                IEnumerable model)
            {
                _key = key;

                _keyEnumerator = elementKeys.GetEnumerator();
                _enumerator = model.GetEnumerator();

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
                if (!_keyEnumerator.MoveNext())
                {
                    return false;
                }

                if (!_enumerator.MoveNext())
                {
                    return false;
                }

                var model = _enumerator.Current;
                var key = ModelNames.CreateIndexModelName(_key, _keyEnumerator.Current);

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
