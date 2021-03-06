﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeOwls.PowerShell.Paths.Processors;
using CodeOwls.PowerShell.Provider.PathNodeProcessors;
using CodeOwls.PowerShell.Provider.PathNodes;
using CodeOwls.ScriptProvider.Persistence;

namespace CodeOwls.ScriptProvider.Provider
{
    class ScriptProviderPathResolver : IPathResolver
    {
        private readonly ScriptProviderDrive _drive;

        public ScriptProviderPathResolver(ScriptProviderDrive drive)
        {
            _drive = drive;
        }

        public IEnumerable<IPathNode> ResolvePath(IProviderContext context, string path)
        {
            context.WriteDebug(String.Format("Resolving path [{0}] drive [{1}]", path, context.Drive));
            string scriptPath = Regex.Replace(path, @"^[^::]+::", String.Empty);
            if (null != context.Drive && !String.IsNullOrEmpty(context.Drive.Root))
            {
                Regex re = new Regex("^.*(" + Regex.Escape(context.Drive.Root) + ")(.*)$", RegexOptions.IgnoreCase );
                var matches = re.Match(path);
                scriptPath = matches.Groups[1].Value;
                path = matches.Groups[2].Value; ;
            }
            else
            {
                Regex re = new Regex("^(.+\\.ps1)(.*)$", RegexOptions.IgnoreCase);
                var matches = re.Match(path);
                scriptPath = matches.Groups[1].Value;
                path = matches.Groups[2].Value;
            }

            IPersistScriptProviderNode persister = null;
            if (null != _drive && null != _drive.Persister )
            {
                persister = _drive.Persister;
            }
            else
            {   
                persister = new ScriptPersister( scriptPath, context );
            }

            var item = persister.Load(path);
            if( null == item )
            {
                var parts = Regex.Split(path, @"[\\\/]+").ToList();
                var leftoverParts = new List<string>();
                var childName = parts.Last();
                parts.RemoveAt( parts.Count - 1 );
                var parentPath = String.Join("\\", parts.ToArray());
                item = persister.Load(parentPath);

                while (null == item)
                {                    
                    leftoverParts.Add(parts.Pop());
                    
                    parentPath = String.Join("\\", parts.ToArray());
                    item = persister.Load(parentPath);
                }

                if (null == item)
                {
                    return null;
                }

                var factory = ItemPathNode.Create(_drive, item);
                if (null == factory)
                {
                    return null;
                }

                while (leftoverParts.Any())
                {
                    var name = leftoverParts.Pop();
                    factory = factory.Resolve(context, name).FirstOrDefault();

                    if (null == factory)
                    {
                        return null;
                    }

                }
                
                return factory.Resolve(context, childName);
            }

            return new[]{ItemPathNode.Create(_drive,item)};
        }
    }
}