using System;
using System.Collections.Generic;
using NUnit.Framework;
public class NineLivesAttemptTests
{
 [Test] public void OriginalDamageBudgetDoesNotPayForGhostRevivalOrOverkill()
 {
  var t=Type.GetType("NineLivesAttemptCredit, Purrbricks.Core"); Assert.That(t,Is.Not.Null);
  var credit=Activator.CreateInstance(t); var brick=new object();
  t.GetMethod("Begin").Invoke(credit,new object[]{new Dictionary<object,int>{{brick,4}}});
  Assert.That(t.GetMethod("Damage").Invoke(credit,new object[]{brick,1}),Is.EqualTo(5));
  Assert.That(t.GetMethod("Damage").Invoke(credit,new object[]{brick,99}),Is.EqualTo(15));
  Assert.That(t.GetMethod("Damage").Invoke(credit,new object[]{brick,4}),Is.EqualTo(0));
 }
}
