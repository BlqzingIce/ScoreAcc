using Zenject;

namespace ScoreAcc
{
    internal class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<LevelStatsViewPatches>().AsSingle();
            Container.BindInterfacesTo<ResultsViewControllerPatches>().AsSingle();
        }
    }

    internal class GameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind<ResultsViewData>().AsSingle().NonLazy();
        }
    }
}