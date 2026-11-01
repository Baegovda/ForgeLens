// FUN_141384090 @ 141384090

/* WARNING: Globals starting with '_' overlap smaller symbols at the same address */

void FUN_141384090(longlong param_1,longlong *param_2,undefined8 *param_3,longlong param_4,
                  longlong param_5)

{
  uint *puVar1;
  uint uVar2;
  longlong lVar3;
  longlong lVar4;
  longlong lVar5;
  undefined2 uVar6;
  int iVar7;
  undefined4 uVar8;
  undefined8 *puVar9;
  undefined1 uVar10;
  byte bVar11;
  uint uVar12;
  char *pcVar13;
  byte bVar14;
  char *pcVar15;
  longlong *plVar16;
  longlong *plVar17;
  undefined1 uVar18;
  float fVar19;
  float fVar20;
  undefined8 *puStack_68;
  int iStack_60;
  longlong *plStack_58;
  int iStack_50;
  
  pcVar15 = (char *)0x0;
  if (iRam0000000144d47f10 < *(int *)(param_4 + 0x718)) {
    pcVar15 = *(char **)(*(longlong *)(param_4 + 0x710) + 8 + (longlong)iRam0000000144d47f10 * 0x10)
    ;
  }
  FUN_141e8aa60(0x144f80c60,*param_2);
  if ((~*(byte *)((longlong)param_3 + 0xf) & 1) == 0) {
    puStack_68 = (undefined8 *)*param_3;
    iStack_60 = *(int *)(param_3 + 1);
  }
  else {
    iStack_60 = 0xf - (uint)(*(byte *)((longlong)param_3 + 0xf) >> 1);
    puStack_68 = param_3;
  }
  FUN_1402db8c0(*param_2 + 0xa8,&puStack_68);
  lVar5 = param_5;
  lVar3 = *(longlong *)(*param_2 + 0x18);
  plVar17 = (longlong *)(param_5 + 0x78);
  plVar16 = plVar17;
  if ((~*(byte *)(param_5 + 0x87) & 1) == 0) {
    plVar16 = (longlong *)*plVar17;
  }
  iVar7 = _stricmp(plVar16,&UNK_14362960c);
  uVar18 = 0;
  if (iVar7 == 0) {
    uVar10 = 1;
  }
  else {
    iVar7 = _stricmp(plVar16,&UNK_143629618);
    uVar10 = uVar18;
    if (iVar7 == 0) {
      uVar10 = 2;
    }
  }
  *(undefined1 *)(lVar3 + 0x6c) = uVar10;
  fVar19 = *(float *)(lVar5 + 0xe0);
  if (*(float *)(lVar5 + 0xe0) < 0.0) {
    fVar19 = _UNK_143c844b4;
  }
  *(float *)(*(longlong *)(*param_2 + 0x18) + 0xd4) = fVar19;
  uVar8 = FUN_141390e00(lVar5 + 0x110);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xd8) = uVar8;
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xdc) = *(undefined4 *)(lVar5 + 0xe4);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xe0) = *(undefined4 *)(lVar5 + 0xe8);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xe4) = *(undefined4 *)(lVar5 + 0xec);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xe8) = *(undefined4 *)(lVar5 + 0xf0);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xec) = *(undefined4 *)(lVar5 + 0xf4);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xf4) = *(undefined4 *)(lVar5 + 0x120);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xf8) = *(undefined4 *)(lVar5 + 0xfc);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xf0) = *(undefined4 *)(lVar5 + 0xf8);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x54) = *(undefined4 *)(lVar5 + 0x94);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0xfc) = *(undefined4 *)(lVar5 + 0x100);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x100) = *(undefined4 *)(lVar5 + 0x104);
  bVar14 = *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d);
  bVar11 = bVar14 | 8;
  if (*(char *)(lVar5 + 0x205) == '\0') {
    bVar11 = bVar14 & 0xf7;
  }
  *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d) = bVar11;
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x11c) = *(undefined1 *)(lVar5 + 0x13c);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x120) = *(undefined4 *)(lVar5 + 0x140);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x124) = *(undefined4 *)(lVar5 + 0x144);
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x128) = *(undefined1 *)(lVar5 + 0x1c0);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 300) = *(undefined4 *)(lVar5 + 0x1c4);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x130) = *(undefined4 *)(lVar5 + 0x1c8);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x134) = *(undefined4 *)(lVar5 + 0x1cc);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x114) = *(undefined4 *)(lVar5 + 300);
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x118) = *(undefined1 *)(lVar5 + 0x130);
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x10c) = *(undefined4 *)(lVar5 + 0x138);
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x209) = *(undefined1 *)(lVar5 + 0x108);
  *(undefined1 *)(*param_2 + 0x69) = *(undefined1 *)(lVar5 + 0xaa);
  if (*(char *)(*(longlong *)(*param_2 + 0x18) + 0x6c) == '\x02') {
    uVar10 = 1;
  }
  else {
    uVar10 = *(undefined1 *)(lVar5 + 0x1f8);
  }
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x20c) = uVar10;
  uVar2 = *(uint *)(*param_2 + 0xc);
  uVar12 = uVar2 | 8;
  if (*(char *)(lVar5 + 0xd5) == '\0') {
    uVar12 = uVar2 & 0xfffffff7;
  }
  *(uint *)(*param_2 + 0xc) = uVar12;
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x20b) = *(undefined1 *)(lVar5 + 0xa4);
  puStack_68 = (undefined8 *)(lVar5 + 0x1e8);
  lVar3 = *(longlong *)(*param_2 + 0x18);
  if ((~*(byte *)(lVar5 + 0x1f7) & 1) == 0) {
    puStack_68 = (undefined8 *)*puStack_68;
    iStack_60 = *(int *)(lVar5 + 0x1f0);
  }
  else {
    iStack_60 = 0xf - (uint)(*(byte *)(lVar5 + 0x1f7) >> 1);
  }
  puVar9 = (undefined8 *)FUN_141dd4880(param_4,&param_5,&puStack_68);
  fVar19 = _UNK_143c84394;
  *(undefined8 *)(lVar3 + 0x90) = *puVar9;
  bVar14 = *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d);
  bVar11 = bVar14 | 0x10;
  if (*(char *)(lVar5 + 0xa9) == '\0') {
    bVar11 = bVar14 & 0xef;
  }
  *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d) = bVar11;
  fVar20 = *(float *)(lVar5 + 0xa0);
  if ((fVar19 < fVar20) && (fVar19 = fVar20, _UNK_143c845d8 <= fVar20)) {
    fVar19 = _UNK_143c845d8;
  }
  *(float *)(*(longlong *)(*param_2 + 0x18) + 0x68) = fVar19;
  lVar3 = *(longlong *)(*param_2 + 0x18);
  if ((~*(byte *)(lVar5 + 0x87) & 1) == 0) {
    plVar17 = (longlong *)*plVar17;
  }
  iVar7 = _stricmp(plVar17,&UNK_14362960c);
  if (iVar7 == 0) {
    uVar18 = 1;
  }
  else {
    iVar7 = _stricmp(plVar17,&UNK_143629618);
    if (iVar7 == 0) {
      uVar18 = 2;
    }
  }
  *(undefined1 *)(lVar3 + 0x6c) = uVar18;
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x20a) = *(undefined1 *)(lVar5 + 0x204);
  bVar14 = *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d);
  bVar11 = bVar14 | 4;
  if (*(char *)(lVar5 + 0x98) == '\0') {
    bVar11 = bVar14 & 0xfb;
  }
  *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d) = bVar11;
  *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x1fc) = *(undefined4 *)(lVar5 + 0x9c);
  if (*(char *)(lVar5 + 0xa6) != '\0') {
    *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x20c) = 3;
  }
  *(uint *)(*param_2 + 0xc) = *(uint *)(*param_2 + 0xc) | 8;
  func_0x000141d7e340(*param_2,0,3);
  if (*(char *)(*(longlong *)(*param_2 + 0x18) + 0x6c) == '\x01') {
    fVar19 = *(float *)(lVar5 + 0x88);
    if (_UNK_1436385ec <= *(float *)(lVar5 + 0x88)) {
      fVar19 = _UNK_1436385ec;
    }
    *(float *)(*(longlong *)(*param_2 + 0x18) + 0x60) = fVar19;
    fVar19 = *(float *)(lVar5 + 0x8c);
    fVar20 = 0.0;
    if ((0.0 < fVar19) && (fVar20 = fVar19, _UNK_14355c0f0 <= fVar19)) {
      fVar20 = _UNK_14355c0f0;
    }
    *(float *)(*(longlong *)(*param_2 + 0x18) + 100) = fVar20;
  }
  bVar14 = *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d);
  bVar11 = bVar14 | 2;
  if (*(char *)(lVar5 + 0x91) == '\0') {
    bVar11 = bVar14 & 0xfd;
  }
  *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d) = bVar11;
  bVar14 = *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d);
  bVar11 = bVar14 | 1;
  if (*(char *)(lVar5 + 0xa5) == '\0') {
    bVar11 = bVar14 & 0xfe;
  }
  *(byte *)(*(longlong *)(*param_2 + 0x18) + 0x6d) = bVar11;
  func_0x00014224fa60(*(undefined8 *)(*param_2 + 0x18),*(undefined4 *)(lVar5 + 200),
                      *(undefined4 *)(lVar5 + 0xcc));
  func_0x00014224f800(*(undefined8 *)(*param_2 + 0x18),*(undefined1 *)(lVar5 + 0x1d0),
                      *(undefined4 *)(lVar5 + 0x1d4),*(undefined4 *)(lVar5 + 0x1d8),
                      *(undefined1 *)(lVar5 + 0x1dc),*(undefined4 *)(lVar5 + 0x1e0),
                      *(undefined4 *)(lVar5 + 0x1e4));
  lVar3 = *(longlong *)(lVar5 + 0x198);
  bVar14 = ~*(byte *)(lVar3 + 0x1f) & 1;
  if (bVar14 == 0) {
    iVar7 = *(int *)(lVar3 + 0x18);
  }
  else {
    iVar7 = 0xf - (uint)(*(byte *)(lVar3 + 0x1f) >> 1);
  }
  if ((iVar7 == 0) || (lVar4 = *(longlong *)(*param_2 + 0x18), *(char *)(lVar4 + 0x6c) != '\x01')) {
    *(undefined2 *)(*(longlong *)(*param_2 + 0x18) + 0x1f8) = 0xffff;
  }
  else {
    plStack_58 = (longlong *)(lVar3 + 0x10);
    if (bVar14 == 0) {
      plStack_58 = *(longlong **)(lVar3 + 0x10);
    }
    if ((~*(byte *)(lVar3 + 0x1f) & 1) == 0) {
      iStack_50 = *(int *)(lVar3 + 0x18);
    }
    else {
      iStack_50 = 0xf - (uint)(*(byte *)(lVar3 + 0x1f) >> 1);
    }
    uVar6 = FUN_141ef7440(pcVar15,*(undefined8 *)(param_1 + 0x58),&plStack_58);
    *(undefined2 *)(lVar4 + 0x1f8) = uVar6;
  }
  lVar3 = *param_2;
  plStack_58 = (longlong *)(lVar5 + 0x1b0);
  if ((~*(byte *)(lVar5 + 0x1bf) & 1) == 0) {
    plStack_58 = (longlong *)*plStack_58;
    iStack_50 = *(int *)(lVar5 + 0x1b8);
  }
  else {
    iStack_50 = 0xf - (uint)(*(byte *)(lVar5 + 0x1bf) >> 1);
  }
  puStack_68 = (undefined8 *)(lVar5 + 0x1a0);
  if ((~*(byte *)(lVar5 + 0x1af) & 1) == 0) {
    puStack_68 = (undefined8 *)*puStack_68;
    iStack_60 = *(int *)(lVar5 + 0x1a8);
  }
  else {
    iStack_60 = 0xf - (uint)(*(byte *)(lVar5 + 0x1af) >> 1);
  }
  FUN_1402db8c0(lVar3 + 0x48,&puStack_68);
  FUN_1402db8c0(lVar3 + 0x58,&plStack_58);
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x1fa) = 0;
  lVar3 = *param_2;
  if (*(char *)(*(longlong *)(lVar3 + 0x18) + 0x6c) == '\x01') {
    pcVar13 = (char *)(lVar3 + 0x48);
    if ((~*(byte *)(lVar3 + 0x57) & 1) == 0) {
      pcVar13 = *(char **)pcVar13;
    }
    if ((pcVar13 == (char *)0x0) || (*pcVar13 == '\0')) {
      pcVar13 = (char *)(lVar3 + 0x58);
      if ((~*(byte *)(lVar3 + 0x67) & 1) == 0) {
        pcVar13 = *(char **)pcVar13;
      }
      if ((pcVar13 == (char *)0x0) || (*pcVar13 == '\0')) goto code_r0x000141384810;
    }
    if (*(short *)(*(longlong *)(lVar3 + 0x18) + 0x1f8) == -1) {
      plStack_58 = (longlong *)&UNK_143629678;
      iStack_50 = -1;
      uVar6 = FUN_141ef7440(pcVar15,*(undefined8 *)(param_1 + 0x58),&plStack_58);
      *(undefined2 *)(*(longlong *)(*param_2 + 0x18) + 0x1f8) = uVar6;
    }
  }
code_r0x000141384810:
  bVar14 = *(byte *)(lVar5 + 0x157);
  if ((~bVar14 & 1) == 0) {
    iVar7 = *(int *)(lVar5 + 0x150);
  }
  else {
    iVar7 = 0xf - (uint)(bVar14 >> 1);
  }
  if (iVar7 == 0) {
    *(undefined4 *)(*(longlong *)(*param_2 + 0x18) + 0x204) = 0xffffffff;
  }
  else {
    plStack_58 = (longlong *)(lVar5 + 0x148);
    if ((~bVar14 & 1) == 0) {
      plStack_58 = (longlong *)*plStack_58;
      iStack_50 = *(int *)(lVar5 + 0x150);
    }
    else {
      iStack_50 = 0xf - (uint)(bVar14 >> 1);
    }
    FUN_141ef75d0(pcVar15,*param_2,&plStack_58);
  }
  uVar2 = *(uint *)(*param_2 + 0xc);
  uVar12 = uVar2 | 0x20;
  if (*(char *)(lVar5 + 0x158) == '\0') {
    uVar12 = uVar2 & 0xffffffdf;
  }
  *(uint *)(*param_2 + 0xc) = uVar12;
  *(undefined1 *)(*(longlong *)(*param_2 + 0x18) + 0x208) = *(undefined1 *)(lVar5 + 0x208);
  if (*(char *)(*(longlong *)(*param_2 + 0x18) + 0x6c) != '\x02') {
    if (*pcVar15 != '\0') {
      FUN_141e8a8b0(0x144f80c60);
    }
    return;
  }
  puVar1 = (uint *)(*param_2 + 0xc);
  *puVar1 = *puVar1 | 0x2000;
  return;
}

