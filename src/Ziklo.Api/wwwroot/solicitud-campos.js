// Campos adicionales del formato físico. Los datos principales del crédito se capturan en solicitar.html.
window.solicitudCampos = [
  { title: 'Identificación y vivienda del solicitante', fields: [
    ['appBirthPlace','Lugar de nacimiento'], ['appBirthDate','Fecha de nacimiento','date'],
    ['appIssuePlace','Lugar de expedición'], ['appIssueDate','Fecha de expedición','date'],
    ['appCivilStatus','Estado civil'], ['appEducation','Nivel de estudios'], ['appDependents','Personas a cargo','number'],
    ['appGender','Género'], ['appHousing','Tipo de vivienda'], ['appAddress','Dirección residencia'],
    ['appNeighborhood','Barrio'], ['appLandline','Teléfono fijo'],
    ['appSpouseName','Nombre del cónyuge'], ['appSpouseId','Documento del cónyuge'],
    ['appSpousePhone','Teléfono del cónyuge'], ['appSpouseEmail','Correo del cónyuge','email']
  ]},
  { title: 'Actividad laboral y finanzas del solicitante', fields: [
    ['appWorkStatus','Situación laboral'], ['appWorkCategory','Tipo de actividad'],
    ['appProfession','Profesión u oficio'], ['appEmployer','Empresa o empleador'],
    ['appWorkStart','Fecha de ingreso','date'], ['appWorkCity','Ciudad del trabajo'],
    ['appWorkDepartment','Departamento del trabajo'], ['appWorkPhone','Teléfono laboral'],
    ['appWorkAddress','Dirección laboral'], ['appWorkNeighborhood','Barrio laboral'],
    ['appWorkEmail','Correo laboral','email'], ['appPosition','Cargo actual'], ['appContract','Tipo de contrato'],
    ['appOtherIncome','Otros ingresos (COP)','number'], ['appOtherIncomeSource','Origen de otros ingresos'],
    ['appExpenses','Gastos y deducciones (COP)','number'], ['appAssets','Activos (COP)','number'],
    ['appLiabilities','Pasivos (COP)','number'], ['appPayrollDeduction','Descuento de nómina (COP)','number'],
    ['appDebtPurchase','Compra de cartera (COP)','number'], ['appDebtEntity','Entidad de compra de cartera']
  ]},
  { title: 'Referencias del solicitante', fields: [
    ['familyName','Referencia familiar · nombres y apellidos'], ['familyPhone','Teléfono familiar'],
    ['familyRelation','Parentesco'], ['familyEmail','Correo familiar','email'],
    ['familyAddress','Dirección familiar'], ['familyNeighborhood','Barrio familiar'],
    ['personalName','Referencia personal · nombres y apellidos'], ['personalPhone','Teléfono personal'],
    ['personalRelation','Relación'], ['personalEmail','Correo personal','email'],
    ['personalAddress','Dirección personal'], ['personalNeighborhood','Barrio personal']
  ]},
  { title: 'Codeudor', fields: [
    ['coName','Nombres y apellidos'], ['coIdType','Tipo de documento'], ['coId','Número de documento'],
    ['coIssuePlace','Lugar de expedición'], ['coIssueDate','Fecha de expedición','date'],
    ['coBirthPlace','Lugar de nacimiento'], ['coBirthDate','Fecha de nacimiento','date'],
    ['coCivilStatus','Estado civil'], ['coEducation','Nivel de estudios'], ['coDependents','Personas a cargo','number'],
    ['coGender','Género'], ['coEmail','Correo electrónico','email'], ['coPhone','Celular'],
    ['coHousing','Tipo de vivienda'], ['coAddress','Dirección'], ['coNeighborhood','Barrio'],
    ['coSpouseName','Nombre del cónyuge'], ['coSpouseId','Documento del cónyuge'],
    ['coSpousePhone','Teléfono del cónyuge'], ['coSpouseEmail','Correo del cónyuge','email'],
    ['coWorkStatus','Situación laboral'], ['coWorkCategory','Tipo de actividad'],
    ['coProfession','Profesión u oficio'], ['coEmployer','Empresa o empleador'],
    ['coWorkStart','Fecha de ingreso','date'], ['coWorkCity','Ciudad del trabajo'],
    ['coWorkDepartment','Departamento del trabajo'], ['coWorkPhone','Teléfono laboral'],
    ['coWorkAddress','Dirección laboral'], ['coWorkNeighborhood','Barrio laboral'],
    ['coWorkEmail','Correo laboral','email'], ['coPosition','Cargo actual'], ['coContract','Tipo de contrato'],
    ['coIncome','Ingresos mensuales (COP)','number'], ['coOtherIncome','Otros ingresos (COP)','number'],
    ['coOtherIncomeSource','Origen de otros ingresos'], ['coExpenses','Gastos y deducciones (COP)','number'],
    ['coAssets','Activos (COP)','number'], ['coLiabilities','Pasivos (COP)','number'],
    ['coFamilyName','Referencia familiar · nombres y apellidos'], ['coFamilyPhone','Teléfono familiar'],
    ['coFamilyRelation','Parentesco'], ['coFamilyEmail','Correo familiar','email'],
    ['coFamilyAddress','Dirección familiar'], ['coFamilyNeighborhood','Barrio familiar'],
    ['coPersonalName','Referencia personal · nombres y apellidos'], ['coPersonalPhone','Teléfono personal'],
    ['coPersonalRelation','Relación'], ['coPersonalEmail','Correo personal','email'],
    ['coPersonalAddress','Dirección personal'], ['coPersonalNeighborhood','Barrio personal']
  ]}
];
