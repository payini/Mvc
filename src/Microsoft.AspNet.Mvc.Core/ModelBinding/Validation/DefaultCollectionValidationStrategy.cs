// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNet.Mvc.ModelBinding.Validation
{
    public class DefaultCollectionValidationStrategy : IValidationStrategy
    {
        public static readonly IValidationStrategy Instance = new DefaultCollectionValidationStrategy();

        private DefaultCollectionValidationStrategy()
        {
        }

        public IEnumerator<ValidationEntry> GetChildren(
            ModelMetadata metadata,
            string key,
            object model)
        {
            return new Enumerator(metadata.ElementMetadata, key, (IEnumerable)model);
        }

        private class Enumerator : IEnumerator<ValidationEntry>
        {
            private readonly ValidationEntry _entry;
            private readonly string _key;
            private readonly IEnumerable _model;
            private readonly IEnumerator _enumerator;

            private int _index;

            public Enumerator(
                ModelMetadata metadata,
                string key,
                IEnumerable model)
            {
                _key = key;
                _model = model;

                _enumerator = _model.GetEnumerator();
                _entry = new ValidationEntry()
                {
                    Metadata = metadata,
                };

                _index = -1;
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
                _index++;
                if (!_enumerator.MoveNext())
                {
                    return false;
                }

                var key = ModelNames.CreateIndexModelName(_key, _index);
                var model = _enumerator.Current;

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
