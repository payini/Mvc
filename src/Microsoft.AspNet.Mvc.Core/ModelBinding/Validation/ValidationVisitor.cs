﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Framework.Internal;

namespace Microsoft.AspNet.Mvc.ModelBinding.Validation
{
    public class ValidationVisitor
    {
        private readonly IModelValidatorProvider _validatorProvider;
        private readonly IList<IExcludeTypeValidationFilter> _excludeFilters;
        private readonly ModelStateDictionary _modelState;
        private readonly ValidationStateDictionary _validationState;

        private object _container;
        private string _key;
        private object _model;
        private ModelMetadata _metadata;
        private IValidationStrategy _strategy;

        private HashSet<object> _currentPath;

        public ValidationVisitor(
            [NotNull] IModelValidatorProvider validatorProvider,
            [NotNull] IList<IExcludeTypeValidationFilter> excludeFilters,
            [NotNull] ModelStateDictionary modelState,
            [NotNull] ValidationStateDictionary validationState)
        {
            _validatorProvider = validatorProvider;
            _excludeFilters = excludeFilters;
            _modelState = modelState;
            _validationState = validationState;

            _currentPath = new HashSet<object>(ReferenceEqualityComparer.Instance);
        }

        public bool Validate(ModelMetadata metadata, string key, object model)
        {
            if (model == null)
            {
                if (_modelState.GetValidationState(key) != ModelValidationState.Valid)
                {
                    _modelState.MarkFieldValid(key);
                }

                return true;
            }

            var entry = GetValidationEntry(model);
            key = entry?.Key ?? key ?? string.Empty;
            metadata = entry?.Metadata ?? metadata;

            if ((entry != null && entry.SuppressValidation) || _modelState.HasReachedMaxErrors)
            {
                SuppressValidation(key);
                return false;
            }
            else
            {
                return Visit(metadata, key, model, entry?.Strategy);
            }
        }

        protected virtual bool ValidateNode()
        {
            var validators = GetValidators(_metadata);

            var count = validators.Count;
            if (count > 0)
            {
                var context = new ModelValidationContext()
                {
                    Container = _container,
                    Model = _model,
                    Metadata = _metadata,
                };

                var results = new List<ModelValidationResult>();
                for (var i = 0; i < count; i++)
                {
                    results.AddRange(validators[i].Validate(context));
                }

                var resultsCount = results.Count;
                for (var i = 0; i < resultsCount; i++)
                {
                    var result = results[i];
                    var key = ModelNames.CreatePropertyModelName(_key, result.MemberName);
                    _modelState.TryAddModelError(key, result.Message);
                }
            }

            var state = _modelState.GetFieldValidationState(_key);
            if (state == ModelValidationState.Invalid)
            {
                return false;
            }
            else
            {
                // If the field has an entry in ModelState, then record it as valid. Don't create
                // extra entries if they don't exist already.
                var entry = _modelState[_key];
                if (entry != null)
                {
                    entry.ValidationState = ModelValidationState.Valid;
                }

                return true;
            }
        }

        private bool Visit(ModelMetadata metadata, string key, object model, IValidationStrategy strategy)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();

            if (model != null && !_currentPath.Add(model))
            {
                // This is a cycle, bail.
                return true;
            }

            using (Recursifier.DoTheNeedful(this, key, metadata, model, strategy))
            {
                if (_metadata.IsEnumerableType)
                {
                    return VisitEnumerableType();
                }
                else if (_metadata.IsComplexType)
                {
                    return VisitComplexType();
                }
                else
                {
                    return VisitSimpleType();
                }
            }
        }

        private bool VisitEnumerableType()
        {
            var isValid = true;

            if (_model != null)
            {
                var strategy = _strategy ?? DefaultCollectionValidationStrategy.Instance;
                var enumerator = strategy.GetChildren(_metadata, _key, _model);

                for (var i = 0; enumerator.MoveNext(); i++)
                {
                    var model = enumerator.Current.Model;

                    var entry = GetValidationEntry(model);
                    var key = entry?.Key ?? enumerator.Current.Key;
                    var metadata = entry?.Metadata ?? enumerator.Current.Metadata;

                    if ((entry != null && entry.SuppressValidation) || _modelState.HasReachedMaxErrors)
                    {
                        SuppressValidation(key);
                    }
                    else if (!Visit(metadata, key, model, entry?.Strategy))
                    {
                        isValid = false;
                    }
                }
            }

            // Double-checking HasReachedMaxErrors just in case this model has no properties.
            if (isValid && !_modelState.HasReachedMaxErrors)
            {
                isValid &= ValidateNode();
            }

            return isValid;
        }

        private bool VisitComplexType()
        {
            var isValid = true;

            if (_model != null && ShouldValidateProperties(_metadata))
            {
                var strategy = _strategy ?? DefaultComplexObjectValidationStrategy.Instance;
                var enumerator = strategy.GetChildren(_metadata, _key, _model);

                while (enumerator.MoveNext())
                {
                    var model = enumerator.Current.Model;

                    var entry = GetValidationEntry(model);
                    var metadata = entry?.Metadata ?? enumerator.Current.Metadata;
                    var key = entry?.Key ?? enumerator.Current.Key;

                    if ((entry != null && entry.SuppressValidation) || _modelState.HasReachedMaxErrors)
                    {
                        SuppressValidation(key);
                    }
                    else if (!Visit(metadata, key, model, entry?.Strategy))
                    {
                        isValid = false;
                    }
                }
            }
            else if (_model != null)
            {
                SuppressValidation(_key);
            }

            // Double-checking HasReachedMaxErrors just in case this model has no properties.
            if (isValid && !_modelState.HasReachedMaxErrors)
            {
                isValid &= ValidateNode();
            }

            return isValid;
        }

        private bool VisitSimpleType()
        {
            if (_modelState.HasReachedMaxErrors)
            {
                SuppressValidation(_key);
                return false;
            }

            return ValidateNode();
        }

        private IList<IModelValidator> GetValidators(ModelMetadata metadata)
        {
            var context = new ModelValidatorProviderContext(metadata);
            _validatorProvider.GetValidators(context);
            return context.Validators.OrderBy(v => v, ValidatorOrderComparer.Instance).ToList();
        }

        private void SuppressValidation(string key)
        {
            var entries = _modelState.FindKeysWithPrefix(key);
            foreach (var entry in entries)
            {
                entry.Value.ValidationState = ModelValidationState.Skipped;
            }
        }

        private bool ShouldValidateProperties(ModelMetadata metadata)
        {
            var count = _excludeFilters.Count;
            for (var i = 0; i < _excludeFilters.Count; i++)
            {
                if (_excludeFilters[i].IsTypeExcluded(metadata.UnderlyingOrModelType))
                {
                    return false;
                }
            }

            return true;
        }

        private ValidationState GetValidationEntry(object model)
        {
            if (model == null || _validationState == null)
            {
                return null;
            }

            ValidationState entry;
            _validationState.TryGetValue(model, out entry);
            return entry;
        }

        private struct Recursifier : IDisposable
        {
            private readonly ValidationVisitor _visitor;
            private readonly object _container;
            private readonly string _key;
            private readonly ModelMetadata _metadata;
            private readonly object _model;
            private readonly object _newModel;
            private readonly IValidationStrategy _strategy;

            public static Recursifier DoTheNeedful(
                ValidationVisitor visitor,
                string key,
                ModelMetadata metadata,
                object model,
                IValidationStrategy strategy)
            {
                var recursifier = new Recursifier(visitor, model);

                visitor._container = visitor._model;
                visitor._key = key;
                visitor._metadata = metadata;
                visitor._model = model;
                visitor._strategy = strategy;

                return recursifier;
            }

            public Recursifier(ValidationVisitor visitor, object newModel)
            {
                _visitor = visitor;
                _newModel = newModel;

                _container = _visitor._container;
                _key = _visitor._key;
                _metadata = _visitor._metadata;
                _model = _visitor._model;
                _strategy = _visitor._strategy;
            }

            public void Dispose()
            {
                _visitor._container = _container;
                _visitor._key = _key;
                _visitor._metadata = _metadata;
                _visitor._model = _model;
                _visitor._strategy = _strategy;

                _visitor._currentPath.Remove(_newModel);
            }
        }

        // Sorts validators based on whether or not they are 'required'. We want to run
        // 'required' validators first so that we get the best possible error message.
        private class ValidatorOrderComparer : IComparer<IModelValidator>
        {
            public static readonly ValidatorOrderComparer Instance = new ValidatorOrderComparer();

            public int Compare(IModelValidator x, IModelValidator y)
            {
                var xScore = x.IsRequired ? 0 : 1;
                var yScore = y.IsRequired ? 0 : 1;
                return xScore.CompareTo(yScore);
            }
        }
    }
}
