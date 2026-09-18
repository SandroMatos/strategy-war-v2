using System;

namespace LastWars.Client.Editor
{
    public static class ProductionRulesTests
    {
        public static void Main() { Run(); Console.WriteLine("PASS: 12 production and upgrade checks"); }
        public static void Run()
        {
            var p = new ProductionDto { building_type="iron_mine", local_capacity=1000,
                stored_resources=new ResourcesDto(), production_per_hour=new ResourcesDto { iron=100 } };
            Check(ProductionRules.Evaluate(p,0).Seconds==36,"first-unit duration");
            var half=ProductionRules.Evaluate(p,18);
            Check(half.Seconds==18 && Math.Abs(half.Progress-.5f)<.001 && !half.Ready,"half progress");
            Check(ProductionRules.Evaluate(p,35.9).Seconds==1 && !ProductionRules.Evaluate(p,35.9).Ready,"round seconds up");
            Check(ProductionRules.Evaluate(p,36).Ready,"ready at boundary");
            Check(ProductionRules.Evaluate(p,-20).Seconds==36,"negative elapsed");
            p.stored_resources.iron=1;
            Check(ProductionRules.Evaluate(p,0).Ready,"stored units immediately ready");
            p.stored_resources.iron=0;p.production_per_hour.iron=0;
            Check(!ProductionRules.Evaluate(p,10000).Active,"zero production");
            Check(!ProductionRules.Evaluate(null,10000).Ready,"missing snapshot");
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
