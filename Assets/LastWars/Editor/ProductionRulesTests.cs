using System;

namespace LastWars.Client.Editor
{
    public static class ProductionRulesTests
    {
        public static void Main() { Run(); Console.WriteLine("PASS: 17 production and upgrade checks"); }
        public static void Run()
        {
            var p = new ProductionDto { building_type="iron_mine", local_capacity=1000,
                stored_resources=new ResourcesDto(), production_per_hour=new ResourcesDto { iron=100 } };
            Check(ProductionRules.Evaluate(p,0).Seconds==3600,"100-unit duration");
            var half=ProductionRules.Evaluate(p,1800);
            Check(half.Seconds==1800 && Math.Abs(half.Progress-.5f)<.001 && !half.Ready,"half progress");
            Check(ProductionRules.Evaluate(p,3599.9).Seconds==1 && !ProductionRules.Evaluate(p,3599.9).Ready,"round seconds up");
            Check(ProductionRules.Evaluate(p,3600).Ready,"ready at boundary");
            Check(ProductionRules.Evaluate(p,-20).Seconds==3600,"negative elapsed");
            p.stored_resources.iron=100;
            Check(ProductionRules.Evaluate(p,0).Ready,"stored units immediately ready");
            p.stored_resources.iron=50;
            Check(ProductionRules.Evaluate(p,0).Seconds==1800 && ProductionRules.Evaluate(p,0).Progress==.5f,"partial stored stock");
            p.stored_resources.iron=1;
            Check(!ProductionRules.Evaluate(p,0).Ready,"one unit does not unlock collection");
            p.stored_resources.iron=0;p.production_per_hour.iron=0;
            Check(!ProductionRules.Evaluate(p,10000).Active,"zero production");
            Check(!ProductionRules.Evaluate(null,10000).Ready,"missing snapshot");
            p.production_per_hour.iron=3600; p.collection_target=95;
            Check(ProductionRules.Evaluate(p,0).Seconds==95,"server target below 100");
            Check(!ProductionRules.Evaluate(p,94.9).Ready && ProductionRules.Evaluate(p,95).Ready,"server target boundary");
            p.collection_target=105;p.stored_resources.iron=100;
            Check(!ProductionRules.Evaluate(p,0).Ready && ProductionRules.Evaluate(p,0).Seconds==5,"server target above 100");
            var state=new BaseDto { resources=new ResourcesDto { iron=50 },available_builders=1 };
            var quote=new UpgradeDto { required_resources=new ResourcesDto { iron=50 } };
            Check(ProductionRules.UpgradeBlocker(state,quote,true)==null,"exact balance");
            quote.required_resources.iron=51;
            Check(ProductionRules.UpgradeBlocker(state,quote,true)!=null,"insufficient balance");
            quote.required_resources.iron=50;state.available_builders=0;
            Check(ProductionRules.UpgradeBlocker(state,quote,true)!=null,"busy builders");
            state.available_builders=1;
            Check(ProductionRules.UpgradeBlocker(state,quote,false)!=null,"stale balance");
        }
        static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    }
}
