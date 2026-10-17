using System;
using System.Linq;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        // These unit tests check bookkeeping after a caller-supplied proof, not
        // geometric redundancy. The existing oblique-boundary tests exercise
        // the actual planner's topology proof.
        private static DimensionPlan CreateReviewedCoveragePlan(
            out PlannedDimension residual, out PlannedDimension provider,
            string generationRule = "BoundaryInformation.ObliqueProjection",
            string generationProof = "GenerationProof|BoundaryEdge=real-edge-a|RealEndpoints=True")
        {
            residual = CreateCoverageTestDimension("ReviewedBoundaryRequirement");
            residual.RuleId = generationRule;
            residual.TopologyEvidence = generationProof;
            residual.AttachmentValidity = true;
            provider = CreateCoverageTestDimension("LiveProvider", 25.0);
            provider.SourceGeometryIds.Clear();
            provider.SourceGeometryIds.Add("real-edge-b");
            provider.AttachmentValidity = true;
            var plan = new DimensionPlan();
            plan.Add(residual);
            plan.Add(provider);
            return plan;
        }

        private static void ReviewedStructureCoveragePreservesGenerationRuleAndEvidence()
        {
            const string coverageRule = "BoundaryInformation.Covered";
            foreach (string generationRule in new[] {
                "BoundaryInformation.ObliqueProjection", "BoundaryInformation.ChamferSpan" })
            {
                PlannedDimension residual, provider;
                var plan = CreateReviewedCoveragePlan(out residual, out provider, generationRule);
                string originalProof = residual.TopologyEvidence;
                var originalFirst = residual.FirstPoint;
                var originalSecond = residual.SecondPoint;
                var originalSide = residual.Side;
                Assert(plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { provider },
                    new[] { "real-edge-b" }, "CheckedCoverageProof|ConnectedPath=True", generationRule),
                    "reviewed coverage must suppress without rewriting the creation rule");
                plan.CaptureFinalDimensions();
                var diagnostic = plan.Diagnostics.DimensionCandidates.Single(d => d.Id == residual.DiagnosticId);
                Assert(residual.RuleId == generationRule && diagnostic.RuleId == generationRule,
                    "reviewed coverage must preserve the candidate and diagnostic creation RuleId");
                Assert(diagnostic.IsSuppressed && !diagnostic.IsSelected
                    && diagnostic.Decision == DimensionCandidateDecision.Suppressed
                    && diagnostic.SuppressedReason == coverageRule && diagnostic.DecisionReason == coverageRule,
                    "coverage is a suppression decision, not a replacement creation identity");
                Assert(residual.TopologyEvidence.Contains(originalProof)
                    && diagnostic.TopologyEvidence.Contains(originalProof)
                    && diagnostic.TopologyEvidence.Contains("CheckedCoverageProof|ConnectedPath=True")
                    && diagnostic.TopologyEvidence.Contains("CoveredByIds=" + provider.DiagnosticId),
                    "both creation proof and actual coverage providers must remain traceable");
                Assert(diagnostic.SourceGeometryIds.Contains("real-edge-a")
                    && diagnostic.SourceGeometryIds.Contains("real-edge-b"),
                    "coverage evidence must merge rather than erase source geometry");
                Assert(residual.FirstPoint.Equals(originalFirst) && residual.SecondPoint.Equals(originalSecond)
                    && residual.Side == originalSide && residual.AttachmentValidity == true,
                    "bookkeeping recovery must not move geometry or change attachment validity");
                Assert(!plan.Dimensions.Contains(residual) && plan.Dimensions.Contains(provider)
                    && plan.Diagnostics.FinalDimensions.All(d => d.Id != residual.DiagnosticId),
                    "only the proven covered candidate may leave the final plan");
                bool rejected = false;
                try { plan.RecordRuleEvidence(residual, "Unrelated.Reassignment"); }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && residual.RuleId == generationRule && diagnostic.RuleId == generationRule,
                    "ordinary conflicting RuleId assignments must still throw after reviewed coverage");
            }
        }

        private static void ReviewedStructureCoverageStillRequiresReviewAndLiveProof()
        {
            PlannedDimension residual, provider;
            var plan = CreateReviewedCoveragePlan(out residual, out provider);
            const string coverageRule = "BoundaryInformation.Covered";
            string originalRule = residual.RuleId;
            string originalProof = residual.TopologyEvidence;
            foreach (string review in new[] { null, "Unrelated.Rule" })
            {
                Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { provider },
                    new[] { "real-edge-b" }, "CheckedCoverageProof", review),
                    "an existing creation rule must still require explicit matching review");
            }
            Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { provider },
                new[] { "real-edge-b" }, null, originalRule), "matching review is not topology proof");
            Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule, new PlannedDimension[0],
                new[] { "real-edge-b" }, "CheckedCoverageProof", originalRule), "matching review needs a live provider");
            Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { residual },
                new[] { "real-edge-a" }, "CheckedCoverageProof", originalRule), "self coverage remains prohibited");
            Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule,
                new[] { CreateCoverageTestDimension("AbsentProvider") }, new[] { "real-edge-b" },
                "CheckedCoverageProof", originalRule), "providers outside the plan remain prohibited");
            provider.AttachmentValidity = false;
            Assert(!plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { provider },
                new[] { "real-edge-b" }, "CheckedCoverageProof", originalRule),
                "review must not authorize an invalid provider");
            var diagnostic = plan.Diagnostics.DimensionCandidates.Single(d => d.Id == residual.DiagnosticId);
            Assert(plan.Dimensions.Contains(residual) && !diagnostic.IsSuppressed
                && residual.RuleId == originalRule && diagnostic.RuleId == originalRule
                && residual.TopologyEvidence == originalProof && diagnostic.TopologyEvidence == originalProof
                && !diagnostic.SourceGeometryIds.Contains("real-edge-b"),
                "failed coverage must not mutate candidate identity, proof, sources or suppression state");
        }

        private static void ReviewedStructureCoverageStillTracksMissingProviders()
        {
            PlannedDimension residual, provider;
            var plan = CreateReviewedCoveragePlan(out residual, out provider);
            string generationRule = residual.RuleId;
            Assert(plan.TrySuppressCoveredStructure(residual, "BoundaryInformation.Covered", new[] { provider },
                new[] { "real-edge-b" }, "CheckedCoverageProof", generationRule), "reviewed dependency setup must succeed");
            plan.CaptureFinalDimensions();
            Assert(!plan.Diagnostics.Warnings.Any(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)),
                "a live reviewed coverage provider must not produce a dependency gap");
            plan.Dimensions.Remove(provider);
            plan.CaptureFinalDimensions();
            string warning = plan.Diagnostics.Warnings.Single(w => w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal));
            Assert(warning.Contains("Status=PendingConfirmation")
                && warning.Contains("MissingCoveredByIds=" + provider.DiagnosticId)
                && plan.Diagnostics.StructureCoverageStatus == "PendingConfirmation",
                "provider removal must remain visible after preserving the creation identity");
            Assert(!plan.Dimensions.Contains(residual) && residual.RuleId == generationRule,
                "dependency auditing must not silently invent a replacement or change its creation rule");
        }

        private static void StructureCoverageWithoutGenerationRuleKeepsLegacyIdentity()
        {
            PlannedDimension residual, provider;
            var plan = CreateReviewedCoveragePlan(out residual, out provider,
                null, "OriginalLegacyGeometryProof");
            const string coverageRule = "Test.LegacyCoverage";
            Assert(plan.TrySuppressCoveredStructure(residual, coverageRule, new[] { provider },
                new[] { "real-edge-b" }, "CheckedLegacyCoverageProof"), "legacy unruled coverage must still succeed");
            var diagnostic = plan.Diagnostics.DimensionCandidates.Single(d => d.Id == residual.DiagnosticId);
            Assert(residual.RuleId == coverageRule && diagnostic.RuleId == coverageRule
                && diagnostic.SuppressedReason == coverageRule,
                "first rule assignment on an unruled candidate must retain legacy behavior");
            Assert(diagnostic.TopologyEvidence.Contains("OriginalLegacyGeometryProof")
                && diagnostic.TopologyEvidence.Contains("CheckedLegacyCoverageProof"),
                "an unruled candidate's original geometry proof must also survive coverage");
        }
    }
}
