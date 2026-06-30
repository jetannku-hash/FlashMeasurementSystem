using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.MetrologyModel;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// Wave-0 占位：此檔在 02-02（HALCON 適配器 wave）填入真正的合成影像擬合斷言
    /// （HalconMetrologyModelRunner，各 Shape 容差帶 + 多物件單次 Apply）。
    /// 目前僅證明 Domain 量測模型型別可從本專案參照並編譯。
    /// 尚未參照 HalconMetrologyModelRunner（它要到 02-02 才存在）。
    /// </summary>
    public static class MetrologyModelHalconTests
    {
        public static void Run()
        {
            var model = new MetrologyModelDef
            {
                Objects = new List<MetrologyObjectDef>
                {
                    new MetrologyObjectDef { Shape = MetrologyObjectType.Circle, Radius = 50.0 }
                }
            };
            if (model.Objects.Count != 1)
                throw new InvalidOperationException("MetrologyModelHalconTests stub: model build failed");

            Console.WriteLine("MetrologyModelHalconTests (stub) passed");
        }
    }
}
