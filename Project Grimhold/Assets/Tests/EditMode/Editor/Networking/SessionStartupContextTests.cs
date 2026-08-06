using System;
using NUnit.Framework;

namespace ProjectGrimhold.Tests.Networking
{
    public class SessionStartupContextTests
    {
        [Test]
        public void FreshSession_EnablesExpectedBootstrapOperations()
        {
            var context = SessionStartupContext.FreshSession;

            Assert.IsTrue(context.IsValid, "FreshSession should be valid.");
            Assert.AreEqual(SessionStartupMode.FreshSession, context.Mode);
            Assert.IsTrue(context.ShouldExecuteHostBootstrap, "FreshSession should enable host bootstrap.");
            Assert.IsTrue(context.ShouldInitializeMatchPhase, "FreshSession should enable match phase initialization.");
            Assert.IsTrue(context.ShouldExecuteInitialSceneBootstrap, "FreshSession should enable initial scene bootstrap.");
        }

        [Test]
        public void HostMigrationResume_DisablesExpectedBootstrapOperations()
        {
            var context = SessionStartupContext.HostMigrationResume;

            Assert.IsTrue(context.IsValid, "HostMigrationResume should be valid.");
            Assert.AreEqual(SessionStartupMode.HostMigrationResume, context.Mode);
            Assert.IsFalse(context.ShouldExecuteHostBootstrap, "Resume should block host bootstrap.");
            Assert.IsFalse(context.ShouldInitializeMatchPhase, "Resume should block match phase initialization.");
            Assert.IsFalse(context.ShouldExecuteInitialSceneBootstrap, "Resume should block initial scene bootstrap.");
        }

        [Test]
        public void DefaultContext_IsInvalid()
        {
            var context = new SessionStartupContext();

            Assert.IsFalse(context.IsValid, "Default uninitialized context should be invalid.");
            Assert.AreEqual(SessionStartupMode.None, context.Mode);
        }

        [Test]
        public void Constructor_WithInvalidMode_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new SessionStartupContext(SessionStartupMode.None));
            Assert.Throws<ArgumentException>(() => new SessionStartupContext((SessionStartupMode)999));
        }

        [Test]
        public void Contexts_AreIndependent()
        {
            var fresh = SessionStartupContext.FreshSession;
            var resume = SessionStartupContext.HostMigrationResume;

            Assert.AreNotEqual(fresh.Mode, resume.Mode);
            Assert.IsTrue(fresh.ShouldExecuteHostBootstrap);
            Assert.IsFalse(resume.ShouldExecuteHostBootstrap);
        }
    }
}
