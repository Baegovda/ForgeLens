//==== 0x14108ad40

undefined8 FUN_14108ad40(undefined8 param_1,longlong param_2,undefined8 param_3,undefined8 param_4)

{
  undefined8 uVar1;
  undefined1 auStackX_10 [8];
  undefined1 auStack_48 [8];
  undefined1 auStack_40 [8];
  undefined *puStack_38;
  undefined4 uStack_30;
  undefined *puStack_28;
  undefined4 uStack_20;
  
  func_0x00014276d530(auStack_40);
  FUN_142780350(param_4,auStack_40);
  uStack_30 = 7;
  puStack_38 = &UNK_1435f1168;
  uVar1 = FUN_142778200(auStack_40,&puStack_38,0);
  puStack_28 = &UNK_1435f1168;
  uStack_20 = 7;
  FUN_141c3fa90(auStack_48,&puStack_28);
  FUN_142745110(auStackX_10,param_2,auStack_48,uVar1);
  uStack_20 = 7;
  puStack_28 = &UNK_1435f1170;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f1170;
  uStack_30 = 7;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 4,auStack_48,uVar1);
  uStack_20 = 0x11;
  puStack_28 = &UNK_1435f1178;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f1178;
  uStack_30 = 0x11;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745660(auStackX_10,param_2 + 8,auStack_48,uVar1);
  uStack_20 = 0x12;
  puStack_28 = &UNK_1435f1190;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f1190;
  uStack_30 = 0x12;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745660(auStackX_10,param_2 + 0x14,auStack_48,uVar1);
  puStack_28 = &UNK_1435f11a8;
  uStack_20 = 0xd;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f11a8;
  uStack_30 = 0xd;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745130(auStackX_10,param_2 + 0x20,auStack_48,uVar1);
  uStack_20 = 0x18;
  puStack_28 = &UNK_1435f11b8;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f11b8;
  uStack_30 = 0x18;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 0x24,auStack_48,uVar1);
  uStack_20 = 0xc;
  puStack_28 = &UNK_1435f11d8;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f11d8;
  uStack_30 = 0xc;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 0x28,auStack_48,uVar1);
  uStack_20 = 0x18;
  puStack_28 = &UNK_1435f11e8;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f11e8;
  uStack_30 = 0x18;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745130(auStackX_10,param_2 + 0x2c,auStack_48,uVar1);
  FUN_14276d7a0(auStack_40);
  return param_1;
}


//==== 0x14108b4e0

undefined8 FUN_14108b4e0(undefined8 param_1,longlong *param_2,longlong param_3)

{
  int iVar1;
  longlong *plVar2;
  undefined8 uVar3;
  undefined1 auStackX_10 [8];
  undefined1 auStackX_20 [8];
  undefined *puStack_28;
  undefined4 uStack_20;
  
  if (*param_2 != 0) {
    iVar1 = func_0x000142791d50(param_2);
    if (iVar1 != 1) {
      puStack_28 = &UNK_1435f1168;
      uStack_20 = 7;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_20 = 7;
      puStack_28 = &UNK_1435f1170;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 4);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_20 = 0x11;
      puStack_28 = &UNK_1435f1178;
      uVar3 = FUN_142792ed0(param_2,auStackX_20,&puStack_28);
      FUN_142707de0(auStackX_10,uVar3,param_3 + 8);
      FUN_142791b10(auStackX_20);
      uStack_20 = 0x12;
      puStack_28 = &UNK_1435f1190;
      uVar3 = FUN_142792ed0(param_2,auStackX_20,&puStack_28);
      FUN_142707de0(auStackX_10,uVar3,param_3 + 0x14);
      FUN_142791b10(auStackX_20);
      uStack_20 = 0xd;
      puStack_28 = &UNK_1435f11a8;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_1427921b0(plVar2,param_3 + 0x20);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_20 = 0x18;
      puStack_28 = &UNK_1435f11b8;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 0x24);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_20 = 0xc;
      puStack_28 = &UNK_1435f11d8;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 0x28);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_20 = 0x18;
      puStack_28 = &UNK_1435f11e8;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_28);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_1427921b0(plVar2,param_3 + 0x2c);
        }
      }
      FUN_142791b10(auStackX_10);
    }
  }
  return param_1;
}


//==== 0x141089170

undefined8 FUN_141089170(undefined8 param_1,longlong param_2,undefined8 param_3,undefined8 param_4)

{
  undefined8 uVar1;
  undefined1 auStackX_10 [8];
  undefined1 auStack_48 [8];
  undefined1 auStack_40 [8];
  undefined *puStack_38;
  undefined4 uStack_30;
  undefined *puStack_28;
  undefined4 uStack_20;
  
  func_0x00014276d530(auStack_40);
  FUN_142780350(param_4,auStack_40);
  uStack_30 = 9;
  puStack_38 = &UNK_1435f10d0;
  uVar1 = FUN_142778200(auStack_40,&puStack_38,0);
  puStack_28 = &UNK_1435f10d0;
  uStack_20 = 9;
  FUN_141c3fa90(auStack_48,&puStack_28);
  FUN_142745110(auStackX_10,param_2,auStack_48,uVar1);
  uStack_20 = 0x13;
  puStack_28 = &UNK_1435f10e0;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f10e0;
  uStack_30 = 0x13;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 4,auStack_48,uVar1);
  uStack_20 = 0x10;
  puStack_28 = &UNK_1435f10f8;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f10f8;
  uStack_30 = 0x10;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 8,auStack_48,uVar1);
  uStack_20 = 0xc;
  puStack_28 = &UNK_1435f1110;
  uVar1 = FUN_142778200(auStack_40,&puStack_28,0);
  puStack_38 = &UNK_1435f1110;
  uStack_30 = 0xc;
  FUN_141c3fa90(auStack_48,&puStack_38);
  FUN_142745110(auStackX_10,param_2 + 0xc,auStack_48,uVar1);
  FUN_14276d7a0(auStack_40);
  return param_1;
}


//==== 0x141089600

undefined8 FUN_141089600(undefined8 param_1,longlong *param_2,longlong param_3)

{
  int iVar1;
  longlong *plVar2;
  undefined1 auStackX_10 [8];
  undefined *puStack_18;
  undefined4 uStack_10;
  
  if (*param_2 != 0) {
    iVar1 = func_0x000142791d50(param_2);
    if (iVar1 != 1) {
      puStack_18 = &UNK_1435f10d0;
      uStack_10 = 9;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_18);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_10 = 0x13;
      puStack_18 = &UNK_1435f10e0;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_18);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 4);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_10 = 0x10;
      puStack_18 = &UNK_1435f10f8;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_18);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 8);
        }
      }
      FUN_142791b10(auStackX_10);
      uStack_10 = 0xc;
      puStack_18 = &UNK_1435f1110;
      plVar2 = (longlong *)FUN_142792ed0(param_2,auStackX_10,&puStack_18);
      if (*plVar2 != 0) {
        iVar1 = func_0x000142791d50(plVar2);
        if (iVar1 != 1) {
          FUN_142792130(plVar2,param_3 + 0xc);
        }
      }
      FUN_142791b10(auStackX_10);
    }
  }
  return param_1;
}


//==== 0x14108a150

void FUN_14108a150(undefined1 *param_1,undefined4 *param_2)

{
  longlong *plVar1;
  undefined4 uVar2;
  undefined4 uVar3;
  undefined4 uVar4;
  undefined8 uVar5;
  undefined4 *puVar6;
  undefined4 auStackX_8 [2];
  undefined1 auStackX_10 [8];
  undefined *puStack_78;
  undefined4 uStack_70;
  undefined4 uStack_68;
  undefined4 uStack_64;
  undefined4 uStack_60;
  undefined4 uStack_5c;
  undefined4 uStack_58;
  undefined4 uStack_54;
  undefined4 uStack_50;
  undefined4 uStack_4c;
  longlong *plStack_48;
  undefined4 uStack_40;
  undefined4 uStack_3c;
  undefined4 uStack_38;
  undefined4 uStack_34;
  undefined4 uStack_30;
  undefined4 uStack_2c;
  undefined4 uStack_28;
  undefined4 uStack_24;
  longlong *plStack_20;
  
  func_0x00014276d530(auStackX_10);
  auStackX_8[0] = CONCAT31(auStackX_8[0]._1_3_,*param_1);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b40;
  (**(code **)(*plRam00000001450d0b40 + 0x10))(plRam00000001450d0b40,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_143562ad0;
  uStack_70 = 6;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 4);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_1435f1130;
  uStack_70 = 0xb;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 8);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_1435f1140;
  uStack_70 = 0xb;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  puVar6 = (undefined4 *)FUN_142776d60(auStackX_10,&uStack_40);
  if (param_2 != puVar6) {
    plVar1 = *(longlong **)(param_2 + 8);
    if (plVar1 != (longlong *)0x0) {
      (**(code **)(*plVar1 + 0x18))(plVar1,param_2);
    }
    *(undefined8 *)(param_2 + 8) = *(undefined8 *)(puVar6 + 8);
    uVar2 = puVar6[1];
    uVar3 = puVar6[2];
    uVar4 = puVar6[3];
    *param_2 = *puVar6;
    param_2[1] = uVar2;
    param_2[2] = uVar3;
    param_2[3] = uVar4;
    uVar5 = *(undefined8 *)(puVar6 + 6);
    *(undefined8 *)(param_2 + 4) = *(undefined8 *)(puVar6 + 4);
    *(undefined8 *)(param_2 + 6) = uVar5;
    *(undefined8 *)(puVar6 + 8) = 0;
  }
  if (plStack_20 != (longlong *)0x0) {
    (**(code **)(*plStack_20 + 0x18))(plStack_20,&uStack_40);
  }
  FUN_14276d7a0(auStackX_10);
  return;
}


//==== 0x141089ad0

void FUN_141089ad0(undefined1 *param_1,undefined4 *param_2)

{
  longlong *plVar1;
  undefined4 uVar2;
  undefined4 uVar3;
  undefined4 uVar4;
  undefined8 uVar5;
  undefined4 *puVar6;
  undefined4 auStackX_8 [2];
  undefined1 auStackX_10 [8];
  undefined *puStack_78;
  undefined4 uStack_70;
  undefined4 uStack_68;
  undefined4 uStack_64;
  undefined4 uStack_60;
  undefined4 uStack_5c;
  undefined4 uStack_58;
  undefined4 uStack_54;
  undefined4 uStack_50;
  undefined4 uStack_4c;
  longlong *plStack_48;
  undefined4 uStack_40;
  undefined4 uStack_3c;
  undefined4 uStack_38;
  undefined4 uStack_34;
  undefined4 uStack_30;
  undefined4 uStack_2c;
  undefined4 uStack_28;
  undefined4 uStack_24;
  longlong *plStack_20;
  
  func_0x00014276d530(auStackX_10);
  auStackX_8[0] = CONCAT31(auStackX_8[0]._1_3_,*param_1);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b40;
  (**(code **)(*plRam00000001450d0b40 + 0x10))(plRam00000001450d0b40,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_143562ad0;
  uStack_70 = 6;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 4);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_1435f10d0;
  uStack_70 = 9;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 8);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_1435f1120;
  uStack_70 = 0xd;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  puVar6 = (undefined4 *)FUN_142776d60(auStackX_10,&uStack_40);
  if (param_2 != puVar6) {
    plVar1 = *(longlong **)(param_2 + 8);
    if (plVar1 != (longlong *)0x0) {
      (**(code **)(*plVar1 + 0x18))(plVar1,param_2);
    }
    *(undefined8 *)(param_2 + 8) = *(undefined8 *)(puVar6 + 8);
    uVar2 = puVar6[1];
    uVar3 = puVar6[2];
    uVar4 = puVar6[3];
    *param_2 = *puVar6;
    param_2[1] = uVar2;
    param_2[2] = uVar3;
    param_2[3] = uVar4;
    uVar5 = *(undefined8 *)(puVar6 + 6);
    *(undefined8 *)(param_2 + 4) = *(undefined8 *)(puVar6 + 4);
    *(undefined8 *)(param_2 + 6) = uVar5;
    *(undefined8 *)(puVar6 + 8) = 0;
  }
  if (plStack_20 != (longlong *)0x0) {
    (**(code **)(*plStack_20 + 0x18))(plStack_20,&uStack_40);
  }
  FUN_14276d7a0(auStackX_10);
  return;
}


//==== 0x14108bc10

void FUN_14108bc10(undefined1 *param_1,undefined4 *param_2)

{
  longlong *plVar1;
  undefined4 uVar2;
  undefined4 uVar3;
  undefined4 uVar4;
  undefined8 uVar5;
  undefined4 *puVar6;
  undefined4 auStackX_8 [2];
  undefined1 auStackX_10 [8];
  undefined *puStack_78;
  undefined4 uStack_70;
  undefined4 uStack_68;
  undefined4 uStack_64;
  undefined4 uStack_60;
  undefined4 uStack_5c;
  undefined4 uStack_58;
  undefined4 uStack_54;
  undefined4 uStack_50;
  undefined4 uStack_4c;
  longlong *plStack_48;
  undefined4 uStack_40;
  undefined4 uStack_3c;
  undefined4 uStack_38;
  undefined4 uStack_34;
  undefined4 uStack_30;
  undefined4 uStack_2c;
  undefined4 uStack_28;
  undefined4 uStack_24;
  longlong *plStack_20;
  
  func_0x00014276d530(auStackX_10);
  auStackX_8[0] = CONCAT31(auStackX_8[0]._1_3_,*param_1);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b40;
  (**(code **)(*plRam00000001450d0b40 + 0x10))(plRam00000001450d0b40,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_143562ad0;
  uStack_70 = 6;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 4);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_1435f10d0;
  uStack_70 = 9;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  auStackX_8[0] = *(undefined4 *)(param_1 + 8);
  plStack_48 = (longlong *)0x0;
  plStack_20 = plRam00000001450d0b48;
  (**(code **)(*plRam00000001450d0b48 + 0x10))(plRam00000001450d0b48,&uStack_40,auStackX_8);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  plStack_48 = plStack_20;
  puStack_78 = &UNK_143581688;
  uStack_70 = 8;
  uStack_68 = uStack_40;
  uStack_64 = uStack_3c;
  uStack_60 = uStack_38;
  uStack_5c = uStack_34;
  uStack_58 = uStack_30;
  uStack_54 = uStack_2c;
  uStack_50 = uStack_28;
  uStack_4c = uStack_24;
  FUN_142777bd0(auStackX_10,&puStack_78,&uStack_68,0,0xffffffff);
  if (plStack_48 != (longlong *)0x0) {
    (**(code **)(*plStack_48 + 0x18))(plStack_48,&uStack_68);
  }
  puVar6 = (undefined4 *)FUN_142776d60(auStackX_10,&uStack_40);
  if (param_2 != puVar6) {
    plVar1 = *(longlong **)(param_2 + 8);
    if (plVar1 != (longlong *)0x0) {
      (**(code **)(*plVar1 + 0x18))(plVar1,param_2);
    }
    *(undefined8 *)(param_2 + 8) = *(undefined8 *)(puVar6 + 8);
    uVar2 = puVar6[1];
    uVar3 = puVar6[2];
    uVar4 = puVar6[3];
    *param_2 = *puVar6;
    param_2[1] = uVar2;
    param_2[2] = uVar3;
    param_2[3] = uVar4;
    uVar5 = *(undefined8 *)(puVar6 + 6);
    *(undefined8 *)(param_2 + 4) = *(undefined8 *)(puVar6 + 4);
    *(undefined8 *)(param_2 + 6) = uVar5;
    *(undefined8 *)(puVar6 + 8) = 0;
  }
  if (plStack_20 != (longlong *)0x0) {
    (**(code **)(*plStack_20 + 0x18))(plStack_20,&uStack_40);
  }
  FUN_14276d7a0(auStackX_10);
  return;
}


//==== 0x1402365c0

void FUN_1402365c0(void)

{
  return;
}


//==== 0x142dbbaf0

/* WARNING: Globals starting with '_' overlap smaller symbols at the same address */

void FUN_142dbbaf0(longlong *param_1)

{
  float *pfVar1;
  uint *puVar2;
  undefined1 auVar3 [16];
  undefined1 auVar4 [16];
  float *pfVar5;
  float *pfVar6;
  float *pfVar7;
  uint uVar8;
  uint uVar9;
  uint uVar10;
  int iVar11;
  longlong *plVar12;
  undefined8 uVar13;
  float *pfVar14;
  uint uVar15;
  float *pfVar16;
  longlong lVar17;
  longlong lVar18;
  longlong lVar19;
  undefined *puVar20;
  undefined *puVar21;
  undefined1 uVar22;
  ulonglong uVar23;
  float fVar24;
  undefined1 auVar25 [16];
  undefined *puStackX_8;
  longlong lStackX_10;
  undefined8 in_stack_ffffffffffffff68;
  undefined4 uVar26;
  uint uStack_68;
  uint uStack_64;
  uint uStack_60;
  uint uStack_5c;
  float fStack_58;
  float fStack_54;
  float fStack_50;
  float fStack_4c;
  undefined *puStack_48;
  longlong lStack_40;
  
  uVar26 = (undefined4)((ulonglong)in_stack_ffffffffffffff68 >> 0x20);
  FUN_142dbb850();
  if ((*(float *)(param_1[1] + 0xcc) != 0.0) || (*(float *)(param_1[1] + 0xd0) != 0.0)) {
    plVar12 = (longlong *)func_0x000142d99af0();
    uVar13 = 0;
    if (plVar12 == (longlong *)0x0) {
      uVar23 = (ulonglong)puStackX_8 & 0xff;
      puVar20 = puStackX_8;
      puVar21 = puStackX_8;
    }
    else {
      uVar23 = 0;
      uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93de0,0,0);
      puVar20 = &UNK_143b93de0;
      puVar21 = (undefined *)0x0;
    }
    lVar19 = param_1[1];
    uStack_68 = *(uint *)(param_1 + 0x54);
    uStack_64 = *(uint *)((longlong)param_1 + 0x2a4);
    uStack_60 = *(uint *)(param_1 + 0x55);
    uStack_5c = *(uint *)((longlong)param_1 + 0x2ac);
    if (*(char *)((longlong)param_1 + 0x32c) == '\0') {
      FUN_142db8ed0(*(undefined8 *)(lVar19 + 8),*(undefined8 *)(lVar19 + 0x10),
                    *(longlong *)(lVar19 + 0xc0),
                    *(longlong *)(lVar19 + 0xc0) + (ulonglong)(uint)(*(int *)(lVar19 + 200) * 3) * 2
                    ,CONCAT44(uVar26,(int)param_1[0x65]),*(undefined4 *)(lVar19 + 0xcc),
                    *(undefined4 *)(lVar19 + 0xd0),*(undefined4 *)(lVar19 + 0xd4),&uStack_68,
                    param_1 + 0x4a);
    }
    else {
      FUN_142db8ba0();
    }
    if (plVar12 != (longlong *)0x0) {
      (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar23,puVar21);
    }
  }
  fVar24 = _UNK_143c844b4;
  if (*(longlong *)(param_1[1] + 0xd8) != 0) {
    plVar12 = (longlong *)func_0x000142d99af0();
    uVar13 = 0;
    if (plVar12 == (longlong *)0x0) {
      uVar23 = (ulonglong)puStackX_8 & 0xff;
      puVar20 = puStackX_8;
      puVar21 = puStackX_8;
    }
    else {
      uVar23 = 0;
      uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93e08,0,0);
      puVar20 = &UNK_143b93e08;
      puVar21 = (undefined *)0x0;
    }
    puVar2 = (uint *)param_1[1];
    lStackX_10 = *(longlong *)(puVar2 + 0x38);
    lVar19 = *(longlong *)(puVar2 + 2);
    puStackX_8 = *(undefined **)(puVar2 + 0x36);
    uStack_5c = puVar2[0x3a];
    lVar18 = *param_1;
    lVar17 = (ulonglong)*puVar2 * 0x10 + lVar19;
    uStack_68 = (*(uint *)(lVar18 + 400) ^ uStack_5c) & uRam0000000145161a50 ^ uStack_5c;
    uStack_64 = (*(uint *)(lVar18 + 0x194) ^ uStack_5c) & uRam0000000145161a54 ^ uStack_5c;
    uStack_60 = (*(uint *)(lVar18 + 0x198) ^ uStack_5c) & uRam0000000145161a58 ^ uStack_5c;
    uStack_5c = (*(uint *)(lVar18 + 0x19c) ^ uStack_5c) & uRam0000000145161a5c ^ uStack_5c;
    if (lStackX_10 == 0) {
      FUN_142db91c0(lVar19,lVar17,&puStackX_8,&uStack_68);
    }
    else if (*(uint *)(param_1 + 100) == 1) {
      FUN_142db91c0(lVar19,lVar17,&lStackX_10,&uStack_68);
    }
    else {
      fStack_58 = (fVar24 - (float)*(uint *)(param_1 + 100) * *(float *)((longlong)param_1 + 0x324))
                  + *(float *)((longlong)param_1 + 0x324);
      fStack_54 = fStack_58;
      fStack_50 = fStack_58;
      fStack_4c = fStack_58;
      puStack_48 = puStackX_8;
      lStack_40 = lStackX_10;
      FUN_142db9410(lVar19,lVar17,&fStack_58,&uStack_68);
    }
    if (plVar12 != (longlong *)0x0) {
      (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar23,puVar21);
    }
  }
  FUN_142dbb580(param_1);
  FUN_142dbc0b0(param_1);
  if (*(longlong *)(param_1[1] + 0xf0) != 0) {
    plVar12 = (longlong *)func_0x000142d99af0();
    uVar13 = 0;
    if (plVar12 == (longlong *)0x0) {
      uVar23 = (ulonglong)puStackX_8 & 0xff;
      puVar20 = puStackX_8;
      puVar21 = puStackX_8;
    }
    else {
      uVar23 = 0;
      uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93e30,0,0);
      puVar20 = &UNK_143b93e30;
      puVar21 = (undefined *)0x0;
    }
    puVar2 = (uint *)param_1[1];
    lStackX_10 = *(longlong *)(puVar2 + 0x3e);
    lVar19 = *(longlong *)(puVar2 + 2);
    puStackX_8 = *(undefined **)(puVar2 + 0x3c);
    lVar18 = (ulonglong)*puVar2 * 0x10 + lVar19;
    if (lStackX_10 == 0) {
      FUN_142db96c0(lVar19,lVar18,&puStackX_8);
    }
    else if (*(uint *)(param_1 + 100) == 1) {
      FUN_142db96c0(lVar19,lVar18,&lStackX_10);
    }
    else {
      fStack_58 = (fVar24 - (float)*(uint *)(param_1 + 100) * *(float *)((longlong)param_1 + 0x324))
                  + *(float *)((longlong)param_1 + 0x324);
      fStack_54 = fStack_58;
      fStack_50 = fStack_58;
      fStack_4c = fStack_58;
      puStack_48 = puStackX_8;
      lStack_40 = lStackX_10;
      FUN_142db98d0(lVar19,lVar18,&fStack_58);
    }
    if (plVar12 != (longlong *)0x0) {
      (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar23,puVar21);
    }
  }
  plVar12 = (longlong *)func_0x000142d99af0();
  uVar13 = 0;
  if (plVar12 == (longlong *)0x0) {
    uVar23 = (ulonglong)puStackX_8 & 0xff;
    puVar20 = puStackX_8;
    puVar21 = puStackX_8;
  }
  else {
    uVar23 = 0;
    uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93e60,0,0);
    puVar20 = &UNK_143b93e60;
    puVar21 = (undefined *)0x0;
  }
  FUN_142db1180(param_1 + 4,param_1 + 0x4a);
  if (plVar12 != (longlong *)0x0) {
    (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar23,puVar21);
  }
  plVar12 = (longlong *)func_0x000142d99af0();
  uVar13 = 0;
  if (plVar12 == (longlong *)0x0) {
    uVar23 = (ulonglong)puStackX_8 & 0xff;
    puVar20 = puStackX_8;
    puVar21 = puStackX_8;
  }
  else {
    uVar23 = 0;
    uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93e88,0,0);
    puVar20 = &UNK_143b93e88;
    puVar21 = (undefined *)0x0;
  }
  FUN_142db84f0(param_1 + 0x40);
  if (plVar12 != (longlong *)0x0) {
    (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar23,puVar21);
  }
  plVar12 = (longlong *)func_0x000142d99af0();
  uVar13 = 0;
  if (plVar12 == (longlong *)0x0) {
    puVar20 = puStackX_8;
    puVar21 = puStackX_8;
    uVar22 = puStackX_8._0_1_;
  }
  else {
    uVar13 = (**(code **)(*plVar12 + 8))(plVar12,&UNK_143b93eb8,0,0);
    puVar20 = &UNK_143b93eb8;
    puVar21 = (undefined *)0x0;
    uVar22 = 0;
  }
  uVar10 = (uint)(longlong)(*(float *)(param_1 + 0x65) * _UNK_1435571a4);
  uVar15 = 1;
  if (1 < uVar10) {
    uVar15 = uVar10;
  }
  *(int *)(param_1[1] + 0x1bc) = *(int *)(param_1[1] + 0x1bc) + uVar15;
  uVar9 = uRam0000000145161a5c;
  uVar8 = uRam0000000145161a58;
  uVar10 = uRam0000000145161a54;
  uVar15 = uRam0000000145161a50;
  puVar2 = (uint *)param_1[1];
  if (*(uint *)(*param_1 + 0x108) <= puVar2[0x6f]) {
    pfVar14 = *(float **)(puVar2 + 2);
    auVar25 = ZEXT816(0);
    pfVar16 = pfVar14 + (ulonglong)*puVar2 * 4;
    if (pfVar14 < pfVar16) {
      lVar19 = *(longlong *)(puVar2 + 4) - (longlong)pfVar14;
      do {
        fVar24 = *pfVar14;
        pfVar5 = pfVar14 + 1;
        pfVar6 = pfVar14 + 2;
        pfVar7 = pfVar14 + 3;
        pfVar1 = (float *)(lVar19 + (longlong)pfVar14);
        pfVar14 = pfVar14 + 4;
        auVar3._4_4_ = ~_UNK_143c847f4 & (uint)(*pfVar5 - pfVar1[1]);
        auVar3._0_4_ = ~_UNK_143c847f0 & (uint)(fVar24 - *pfVar1);
        auVar3._8_4_ = ~_UNK_143c847f8 & (uint)(*pfVar6 - pfVar1[2]);
        auVar3._12_4_ = ~_UNK_143c847fc & (uint)(*pfVar7 - pfVar1[3]);
        auVar25 = maxps(auVar25,auVar3);
      } while (pfVar14 < pfVar16);
    }
    puVar2[0x6e] = puVar2[0x6e] + 1;
    lVar19 = *param_1;
    fVar24 = *(float *)(lVar19 + 0x110) * *(float *)(param_1 + 0x65);
    auVar4._4_4_ = -(uint)(fVar24 <= (float)(uVar10 & auVar25._4_4_));
    auVar4._0_4_ = -(uint)(fVar24 <= (float)(uVar15 & auVar25._0_4_));
    auVar4._8_4_ = -(uint)(fVar24 <= (float)(uVar8 & auVar25._8_4_));
    auVar4._12_4_ = -(uint)(fVar24 <= (float)(uVar9 & auVar25._12_4_));
    iVar11 = movmskps((int)pfVar14,auVar4);
    if (iVar11 != 0) {
      *(undefined4 *)(param_1[1] + 0x1b8) = 0;
      lVar19 = *param_1;
    }
    *(int *)(param_1[1] + 0x1bc) = *(int *)(param_1[1] + 0x1bc) - *(int *)(lVar19 + 0x108);
  }
  if (plVar12 != (longlong *)0x0) {
    (**(code **)(*plVar12 + 0x10))(plVar12,uVar13,puVar20,uVar22,puVar21);
  }
  return;
}


