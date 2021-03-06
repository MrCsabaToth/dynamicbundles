﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicBundles.Models;
using System.Web.Optimization;

namespace DynamicBundles
{
    public class DynamicBundlesBuilder
    {
        readonly IDynamicBundleCollection _bundles;
        readonly ICacheHelper _cacheHelper;
        readonly BundleFactories _bundleFactories;

        /// <param name="bundles">
        /// The bundle collection that the bundles will be added to.
        ///
        /// Outside tests, this would always be BundleTable.Bundles
        /// </param>
        /// <param name="cacheHelper">
        /// Cachehelper to use.
        /// </param>
        public DynamicBundlesBuilder(IDynamicBundleCollection bundles, ICacheHelper cacheHelper, BundleFactories bundleFactories)
        {
            _bundles = bundles;
            _cacheHelper = cacheHelper;
            _bundleFactories = bundleFactories;
        }

        /// <summary>
        /// Builds the bundles that need to be loaded on the page.
        /// </summary>
        /// <param name="assetDirectoryList">
        /// The directories with assets that need to be included. These directories may have dependencies
        /// on other directories, such as via .nuspec dependencies files and parent directories.
        ///
        /// When a view is processed, its directory is normally added to this list.
        /// </param>
        /// <param name="scriptBundleVirtualPaths">
        /// The virtual paths of the generated script bundles.
        /// </param>
        /// <param name="styleBundleVirtualPaths">
        /// The virtual paths of the generated style bundles.
        /// </param>
        public void Builder(List<AssetPath> assetDirectoryList,
                            out List<string> scriptBundleVirtualPaths,
                            out List<string> styleBundleVirtualPaths)
        {
            var fileListsByAssetType = new FileListsByAssetType();
            var dependencyResolver = new DependencyResolver(_cacheHelper);

            foreach (AssetPath assetDirectory in assetDirectoryList)
            {
                FileListsByAssetType requiredFilesByAssetType = dependencyResolver.GetRequiredFilesForDirectory(assetDirectory);
                fileListsByAssetType.Append(requiredFilesByAssetType);
            }

            // fileListsByAssetType now contains all required files by asset type

            scriptBundleVirtualPaths = CreateBundles(fileListsByAssetType, AssetType.Script, _bundleFactories.ScriptBundleFactory);
            styleBundleVirtualPaths = CreateBundles(fileListsByAssetType, AssetType.StyleSheet, _bundleFactories.StyleBundleFactory);
        }

        private List<string> CreateBundles(FileListsByAssetType fileListsByAssetType, AssetType assetType, Func<string, string[], Bundle> bundleFactory)
        {
            List<AssetPath> files = fileListsByAssetType.GetList(assetType);

            // ignore minified file if non-minified file is found
            for (var i = files.Count - 1; i >= 0; i--)
            {
                var file = files[i];
                var directoryName = Path.GetDirectoryName(file.AbsolutePath);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.AbsolutePath);

                if (directoryName != null && fileNameWithoutExtension.EndsWith(".min"))
                {
                    var extension = Path.GetExtension(file.AbsolutePath);
                    var nonMinifiedFileName = fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - 4) + extension;
                    var nonMinifiedAbsolutePath = Path.Combine(directoryName, nonMinifiedFileName);
                    if (files.Any(ap => ap.AbsolutePath == nonMinifiedAbsolutePath))
                    {
                        files.RemoveAt(i);
                    }
                }
            }

            List<List<AssetPath>> filesByAreaController = RouteHelper.FilePathsSortedByRoute(files);
            List<string> bundleVirtualPaths = BundleHelper.AddFileListsAsBundles(_bundles, filesByAreaController, bundleFactory);
            return bundleVirtualPaths;
        }
    }
}
