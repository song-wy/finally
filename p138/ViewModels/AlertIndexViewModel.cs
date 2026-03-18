using System;
using System.Collections.Generic;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.ViewModels
{
    /// <summary>
    /// 患者端预警触发页视图模型
    /// </summary>
    public class AlertIndexViewModel
    {
        /// <summary>血糖异常记录（高/低血糖）</summary>
        public List<BloodSugarRecord> BloodSugarAlerts { get; set; } = new List<BloodSugarRecord>();

        /// <summary>足压高风险/极高风险记录</summary>
        public List<FootPressureRecord> FootPressureAlerts { get; set; } = new List<FootPressureRecord>();

        /// <summary>伤口异常记录（感染/渗出/发热/异味）</summary>
        public List<WoundRecord> WoundAlerts { get; set; } = new List<WoundRecord>();

        /// <summary>统计起止日期说明</summary>
        public string DateRangeText { get; set; } = "最近30天";
    }
}
