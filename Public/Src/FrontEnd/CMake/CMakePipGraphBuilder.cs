using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Text;
using BuildXL.FrontEnd.Ninja;
using BuildXL.FrontEnd.Ninja.Serialization;
using BuildXL.Utilities;
using BuildXL.FrontEnd.Workspaces.Core;
using BuildXL.FrontEnd.Sdk;
using BuildXL.FrontEnd.Utilities;
using BuildXL.Native.IO;
using BuildXL.Pips;
using BuildXL.Processes;
using BuildXL.Utilities.Collections;
using BuildXL.Utilities.Configuration;
using BuildXL.Utilities.Configuration.Mutable;
using BuildXL.Utilities.Configuration.Resolvers;

namespace BuildXL.FrontEnd.CMake
{
    internal sealed class CMakePipGraphBuilder
    {
        private AbsolutePath m_buildDirectory;
        private NinjaPipConstructor m_pipConstructor;
        private ICMakeResolverSettings m_resolverSettings;
        private ISet<ReportedFileAccess> m_cmakeAccesses;
        private FrontEndContext m_context;
        private AbsolutePath m_cmakeGeneratedDirectory;

        /// <nodoc/>
        public CMakePipGraphBuilder(
            FrontEndContext context,
            FrontEndHost frontEndHost,
            ModuleDefinition moduleDefinition,
            AbsolutePath projectRoot,
            ISet<ReportedFileAccess> cmakeAccesses,
            AbsolutePath specPath,
            QualifierId qualifierId,
            string frontEndName,
            bool suppressDebugFlags,
            ICMakeResolverSettings resolverSettings)
        {
            Contract.Requires(context != null);
            Contract.Requires(frontEndHost != null);
            Contract.Requires(moduleDefinition != null);
            Contract.Requires(projectRoot.IsValid);
            Contract.Requires(specPath.IsValid);
            Contract.Requires(!string.IsNullOrEmpty(frontEndName));

            m_context = context;
            m_cmakeAccesses = cmakeAccesses;
            m_buildDirectory = resolverSettings.ProjectRoot;
            m_cmakeGeneratedDirectory = projectRoot;
            m_resolverSettings = resolverSettings;
            m_pipConstructor = new NinjaPipConstructor(context, frontEndHost, frontEndName, moduleDefinition, qualifierId, resolverSettings.ProjectRoot, specPath, suppressDebugFlags, resolverSettings.UntrackingSettings);
        }

        private string GetCMakePipCommand()
        {
            StringBuilder builder = new StringBuilder("cmake -GNinja");
            if (m_resolverSettings.CacheEntries != null)
            {
                foreach (KeyValuePair<string, string> entry in m_resolverSettings.CacheEntries)
                {
                    builder.Append(entry.Value != null ? $" -D{entry.Key}={entry.Value}" : " $-U{entry.Key}");
                }
            }

            builder.Append($" {m_buildDirectory.ToString(m_context.PathTable)}");
            return builder.ToString();
        }


        internal bool TrySchedulePips(IReadOnlyCollection<NinjaNode> filteredNodes, IReadOnlyCollection<NinjaTarget> initialTargets, QualifierId qualifierId)
        {

            NinjaNode specialCMakeNode = CreateCMakeSpecialNode();
            FileUtilities.DeleteDirectoryContents(m_cmakeGeneratedDirectory.ToString(m_context.PathTable));
            if (!m_pipConstructor.TrySchedulePip(specialCMakeNode, qualifierId, out _, true))
            {
                return false;
            }

            // We get this collection toposorted from the Json
            foreach (NinjaNode n in filteredNodes)
            {
           //     NinjaNode toSchedule = n;
           //     if (initialTargets.Any(t => t.ProducerNode.Dependencies.Contains(n)))
           //     {
                   var toSchedule = AddNodeDependency(n, specialCMakeNode);
          //      }
                if (!m_pipConstructor.TrySchedulePip(toSchedule, qualifierId, out _))
                {
                    // Already logged 
                    return false;
                }
            }

            return true;
        }

        private NinjaNode AddNodeDependency(NinjaNode node, NinjaNode newDependency)
        {
            return new NinjaNode(node.Rule, node.Command, node.Inputs, node.Outputs, node.Dependencies.Union(new [] { newDependency }).ToList(), node.ResponseFile);
        }

        private NinjaNode CreateCMakeSpecialNode()
        {
            var pathTable = m_context.PathTable;
            var inputs = new List<AbsolutePath>();
            var outputs = new List<AbsolutePath>();

            foreach (var access in m_cmakeAccesses)
            {
                string accessPath = access.GetPath(pathTable);
                if (AbsolutePath.TryCreate(pathTable, accessPath, out AbsolutePath path))
                {
           /*         if ((access.RequestedAccess & RequestedAccess.Read) != 0)
                    {
                        if (FileUtilities.Exists(path.ToString(pathTable)) 
                            && path.IsWithin(pathTable, m_buildDirectory)
                            && !path.IsWithin(pathTable, m_cmakeGeneratedDirectory) 
                            && (access.RequestedAccess & RequestedAccess.Write) == 0)
                        {
                            inputs.Add(path);
                        }
                    }
                    */
                    if (!path.Equals(m_cmakeGeneratedDirectory)
                        && (access.RequestedAccess & RequestedAccess.Write) != 0 && path.IsWithin(pathTable, m_cmakeGeneratedDirectory))
                    {
                        outputs.Add(path);
                    }
                }
            }

            return new NinjaNode("CMake Special Pip", GetCMakePipCommand(), new ReadOnlyHashSet<AbsolutePath>(inputs), new ReadOnlyHashSet<AbsolutePath>(outputs), new NinjaNode[] {});
        }


    }
}
