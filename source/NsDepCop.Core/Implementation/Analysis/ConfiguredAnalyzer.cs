﻿using System;
using System.Collections.Generic;
using System.Threading;
using Codartis.NsDepCop.Core.Interface.Analysis;
using Codartis.NsDepCop.Core.Interface.Config;

namespace Codartis.NsDepCop.Core.Implementation.Analysis
{
    /// <summary>
    /// A dependency analyzer that manages its own config.
    /// </summary>
    public class ConfiguredAnalyzer : IConfiguredAnalyzer
    {
        private readonly ReaderWriterLockSlim _configRefreshLock;
        private readonly IConfigProvider _configProvider;
        private readonly IAnalyzerFactory _analyzerFactory;
        private readonly Parsers? _overridingParser;

        private IProjectConfig _config;
        private IDependencyAnalyzer _dependencyAnalyzer;

        public ConfiguredAnalyzer(IConfigProvider configProvider, IAnalyzerFactory analyzerFactory,
            Parsers? overridingParser = null)
        {
            _configRefreshLock = new ReaderWriterLockSlim();
            _configProvider = configProvider;
            _analyzerFactory = analyzerFactory;
            _overridingParser = overridingParser;

            UpdateConfigAndAnalyzer();
        }

        public IProjectConfig Config => _config;
        public ConfigState ConfigState => _configProvider.ConfigState;
        public Exception ConfigException => _configProvider.ConfigException;

        public void Dispose()
        {
            _configRefreshLock?.Dispose();
        }

        public IEnumerable<DependencyViolation> AnalyzeProject(IEnumerable<string> sourceFilePaths, IEnumerable<string> referencedAssemblyPaths)
        {
            _configRefreshLock.EnterReadLock();

            try
            {
                return _dependencyAnalyzer.AnalyzeProject(sourceFilePaths, referencedAssemblyPaths);
            }
            finally
            {
                _configRefreshLock.ExitReadLock();
            }
        }

        public IEnumerable<DependencyViolation> AnalyzeSyntaxNode(ISyntaxNode syntaxNode, ISemanticModel semanticModel)
        {
            _configRefreshLock.EnterReadLock();

            try
            {
                return _dependencyAnalyzer.AnalyzeSyntaxNode(syntaxNode, semanticModel);
            }
            finally
            {
                _configRefreshLock.ExitReadLock();
            }
        }

        public void RefreshConfig()
        {
            _configRefreshLock.EnterWriteLock();

            try
            {
                _configProvider.RefreshConfig();
                UpdateConfigAndAnalyzer();
            }
            finally
            {
                _configRefreshLock.ExitWriteLock();
            }
        }

        private void UpdateConfigAndAnalyzer()
        {
            var oldConfig = _config;
            _config = GetConfig();

            if (oldConfig != _config)
                _dependencyAnalyzer = CreateDependencyAnalyzer();
        }

        private IProjectConfig GetConfig()
        {
            var config = _configProvider.Config;

            if (_overridingParser != null && 
                config != null && 
                config.Parser != _overridingParser.Value)
                config = config.WithParser(_overridingParser.Value);

            return config;
        }

        private IDependencyAnalyzer CreateDependencyAnalyzer()
        {
            return ConfigState == ConfigState.Enabled
                ? _analyzerFactory.CreateDependencyAnalyzer(Config)
                : null;
        }
    }
}
